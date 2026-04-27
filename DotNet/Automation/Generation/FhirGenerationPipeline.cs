using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.ResourceFactories;
using LantanaGroup.Automation.Helpers;
using System.Globalization;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// A streaming generation-and-upload pipeline that processes one patient at a time to
/// prevent OOM conditions with large patient counts. Each patient's FHIR data is:
///   1. Generated in-memory (reusing <see cref="FhirBundleGenerator"/>'s per-patient logic)
///   2. Manifest metadata accumulated from the in-memory objects
///   3. Serialized into transaction bundle chunks
///   4. Uploaded sequentially to the FHIR server (preserving resource dependency order)
///   5. Disposed — no serialized JSON or FHIR objects retained after upload
///
/// Multiple patients are processed concurrently (bounded by <see cref="MaxConcurrentPatients"/>)
/// but each patient's chunks are uploaded in strict sequential order to preserve the
/// FHIR resource dependency chain (Patient ? Encounter ? Observation, etc.).
///
/// Returns a <see cref="GenerationManifest"/> built incrementally during generation,
/// with optional <see cref="QueryPlanAcquisitionSimulator"/> results computed per-patient
/// before FHIR data is discarded.
/// </summary>
public static class FhirGenerationPipeline
{
    private const int MaxEntriesPerBundle = 500;
    private const int MaxConcurrentPatients = 4;

    /// <summary>
    /// Result of a pipeline run. Contains all metadata needed by validators and the
    /// rest of the test orchestration, without retaining any serialized FHIR JSON.
    /// </summary>
    public sealed class PipelineResult
    {
        /// <summary>Ordered patient IDs, same order as the profiles.</summary>
        public required List<string> PatientIds { get; init; }

        /// <summary>
        /// Fully-populated generation manifest built incrementally during the pipeline.
        /// Contains resource keys, counts, profiles, and (when configured) simulated
        /// acquisition results — everything that <see cref="GenerationManifest.Build"/>
        /// would produce from retained bundles.
        /// </summary>
        public required GenerationManifest Manifest { get; init; }

        /// <summary>Total number of transaction bundle chunks uploaded.</summary>
        public int TotalBundlesUploaded { get; init; }
    }

    /// <summary>
    /// Optional configuration for the pipeline's acquisition simulation.
    /// When provided, the pipeline runs <see cref="QueryPlanAcquisitionSimulator"/>
    /// per-patient during generation and stores results in the manifest.
    /// </summary>
    public sealed class AcquisitionSimulationConfig
    {
        public required QueryPlanInput QueryPlan { get; init; }
        public string? ClinicalPeriodStart { get; init; }
        public string? ClinicalPeriodEnd { get; init; }
    }

    /// <summary>
    /// Generates patients with explicit profiles, uploads each patient's data to the FHIR
    /// server as it's generated, and returns a manifest with all metadata needed for validation.
    /// No serialized FHIR JSON is retained after this method returns.
    /// </summary>
    /// <param name="patientIdPrefix">
    /// Human-readable label used as the base of every generated resource ID. The pipeline
    /// appends a short per-run suffix to this value to guarantee that concurrent runs
    /// (e.g. multiple tests queued rapidly from the UI) cannot collide on shared-infrastructure
    /// or per-patient FHIR resource IDs. Use <paramref name="runId"/> to supply a specific suffix.
    /// </param>
    /// <param name="runId">
    /// Optional explicit run identifier appended to <paramref name="patientIdPrefix"/> to form
    /// the effective FHIR ID prefix. When <c>null</c> (default) a fresh short GUID suffix is
    /// generated so every invocation is isolated. Provide a stable value only when reproducing
    /// a specific run for debugging.
    /// </param>
    public static async Task<PipelineResult> GenerateAndUploadAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = FhirBundleGenerator.DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null,
        FhirGenerationConfig? config = null,
        AcquisitionSimulationConfig? acquisitionSimulation = null,
        string? runId = null)
    {
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));
        if (profiles == null || profiles.Count == 0)
            throw new ArgumentException("At least one patient profile is required.", nameof(profiles));

        // Guarantee per-run ID uniqueness so concurrent pipeline invocations never collide
        // on shared-infrastructure or per-patient FHIR resource IDs. Every resource generated
        // downstream (Organization, Location, Practitioner, Medication, Patient, Encounter,
        // Condition, Observation, ...) is derived from this effective prefix via
        // FhirBundleGenerator.SharedIds, so uniqueness here isolates the entire run.
        var effectiveRunId = string.IsNullOrWhiteSpace(runId)
            ? Guid.NewGuid().ToString("N")[..8]
            : runId!.Trim();
        var effectivePrefix = $"{patientIdPrefix}-{effectiveRunId}";
        patientIdPrefix = effectivePrefix;

        var baseSeed = generationSeed.GetValueOrDefault();
        var manifestBuilder = new GenerationManifest.IncrementalBuilder();

        // Parse the simulator's ClinicalPeriodStart/ClinicalPeriodEnd once so per-patient
        // generation can bound encounter dates inside the supplied period (see
        // FhirBundleGenerator.DeriveInpatientEncounterWindow / DeriveOutpatientEncounterWindow).
        // Without bounding, the seed-only encounter scheme can produce encounters
        // whose tail spills past the period end; downstream date filtering then
        // silently drops late-encounter resources while the simulator still counts
        // them as expected — the asymmetry that surfaces as "actual < expected" in
        // any reconciliation step (e.g. ReportAbsManifestValidator on the Link side).
        // Same parse style as QueryPlanAcquisitionSimulator (AssumeUniversal,
        // InvariantCulture) so both layers agree on the window.
        DateTime? generationClinicalPeriodStart = null;
        DateTime? generationClinicalPeriodEnd = null;
        if (acquisitionSimulation != null)
        {
            if (!string.IsNullOrWhiteSpace(acquisitionSimulation.ClinicalPeriodStart)
                && DateTimeOffset.TryParse(acquisitionSimulation.ClinicalPeriodStart,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rs))
            {
                generationClinicalPeriodStart = rs.UtcDateTime;
            }

            if (!string.IsNullOrWhiteSpace(acquisitionSimulation.ClinicalPeriodEnd)
                && DateTimeOffset.TryParse(acquisitionSimulation.ClinicalPeriodEnd,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var re))
            {
                generationClinicalPeriodEnd = re.UtcDateTime;
            }
        }

        var qualifyingAllCount = profiles.Count(p => p.QualifiesForAll(measures));
        var nonQualifyingAllCount = profiles.Count(p => p.QualifiesForNone(measures));
        var mixedEligibilityCount = profiles.Count - qualifyingAllCount - nonQualifyingAllCount;
        output.WriteLine($"[Pipeline] Generating {profiles.Count} profiled patients ({qualifyingAllCount} qualifying-all, " +
                         $"{nonQualifyingAllCount} non-qualifying-all, {mixedEligibilityCount} mixed) " +
                         $"with ~{totalResourcesPerPatient} resources each, prefix='{effectivePrefix}'" +
                         (generationSeed.HasValue ? $", seed={generationSeed.Value}" : string.Empty) + "...");

        // ------------------------------------------------------------------
        // Shared infrastructure — generated once, uploaded first
        // ------------------------------------------------------------------
        var (sharedEntries, sharedPractitionerIds, sharedMedicationIds, ids) = GenerateSharedInfrastructure(patientIdPrefix);

        // Upload shared infrastructure first
        var sharedBundles = ChunkEntries(sharedEntries, "shared", 0);
        output.WriteLine($"[Pipeline] Uploading {sharedBundles.Count} shared infrastructure bundle(s)...");
        await fhirDataLoader.UploadBundlesSequentiallyAsync(output, sharedBundles, "[shared] ");

        // Record shared entries in manifest
        manifestBuilder.AddEntries(string.Empty, sharedEntries);

        // Build shared resource index for acquisition simulation (if configured)
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries = null;
        if (acquisitionSimulation != null)
        {
            sharedSimEntries = BuildResourceIndex(sharedEntries);
        }

        // ------------------------------------------------------------------
        // Per-patient generation + upload (concurrent across patients,
        // sequential within each patient's chunk sequence)
        // ------------------------------------------------------------------
        var patientIds = new string[profiles.Count];
        var totalBundlesUploaded = sharedBundles.Count;
        var completedPatients = 0;
        var semaphore = new SemaphoreSlim(MaxConcurrentPatients, MaxConcurrentPatients);

        var tasks = new System.Threading.Tasks.Task[profiles.Count];
        for (var p = 0; p < profiles.Count; p++)
        {
            var patientIndex = p; // capture for closure
            tasks[p] = System.Threading.Tasks.Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (patientId, profile, bundleCount) = await GenerateAndUploadSinglePatientAsync(
                        output,
                        fhirDataLoader,
                        manifestBuilder,
                        profiles[patientIndex],
                        patientIndex,
                        baseSeed,
                        totalResourcesPerPatient,
                        patientIdPrefix,
                        measures,
                        sharedPractitionerIds,
                        sharedMedicationIds,
                        sharedSimEntries,
                        acquisitionSimulation,
                        generationClinicalPeriodStart,
                        generationClinicalPeriodEnd,
                        config,
                        ids);

                    patientIds[patientIndex] = patientId;
                    Interlocked.Add(ref totalBundlesUploaded, bundleCount);

                    var done = Interlocked.Increment(ref completedPatients);
                    if (done % 50 == 0 || done == profiles.Count)
                        output.WriteLine($"[Pipeline] Progress: {done}/{profiles.Count} patients generated and uploaded.");
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        await System.Threading.Tasks.Task.WhenAll(tasks);

        // Build the final manifest
        var manifest = manifestBuilder.Build(measures);

        output.WriteLine($"[Pipeline] Complete: {profiles.Count} patients, {totalBundlesUploaded} bundles uploaded.");

        return new PipelineResult
        {
            PatientIds = patientIds.ToList(),
            Manifest = manifest,
            TotalBundlesUploaded = totalBundlesUploaded
        };
    }

    /// <summary>
    /// Generates a single patient's FHIR resources, uploads them, accumulates manifest
    /// metadata, optionally runs acquisition simulation, then discards all FHIR data.
    /// </summary>
    private static async Task<(string PatientId, PatientProfile Profile, int BundleCount)> GenerateAndUploadSinglePatientAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        GenerationManifest.IncrementalBuilder manifestBuilder,
        PatientProfile profile,
        int patientIndex,
        int baseSeed,
        int totalResourcesPerPatient,
        string patientIdPrefix,
        IReadOnlyList<ProfiledMeasureType> measures,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries,
        AcquisitionSimulationConfig? acquisitionSimulation,
        DateTime? generationClinicalPeriodStart,
        DateTime? generationClinicalPeriodEnd,
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids)
    {
        var patientSeed = baseSeed + (profile.SeedOffset ?? patientIndex);
        var patientId = ids.PatientId(patientIndex);

        // Generate entries using the same per-patient logic shared with FhirBundleGenerator.
        var entries = GeneratePatientEntries(
            profile, patientIndex, baseSeed, totalResourcesPerPatient, patientIdPrefix,
            sharedPractitionerIds, sharedMedicationIds, measures,
            generationClinicalPeriodStart, generationClinicalPeriodEnd, config, ids);

        var scenario = FhirGenerationCodes.GetScenarioById(profile.ClinicalScenarioId)
                       ?? FhirGenerationCodes.GetScenarioBySeed(patientSeed);

        DateTime encStart, encEnd;
        if (profile.RequiresInpatientEncounter())
        {
            (encStart, encEnd) = FhirBundleGenerator.DeriveInpatientEncounterWindow(
                patientSeed, generationClinicalPeriodStart, generationClinicalPeriodEnd);
        }
        else
        {
            (encStart, encEnd) = FhirBundleGenerator.DeriveOutpatientEncounterWindow(
                patientSeed, generationClinicalPeriodStart, generationClinicalPeriodEnd);
        }

        var encounterId = $"{patientId}-Enc-001";
        var measureEligibilityLabel = string.Join(", ", measures.Select(m =>
        {
            var shortName = m switch
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "ACH",
                ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "ACH-Daily",
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Hypo",
                _ => m.ToString()
            };
            var eligible = profile.QualifiesFor(m) ? "Q" : "NQ";
            return $"{shortName}={eligible}";
        }));

        output.WriteLine($"  Patient {patientId}: {entries.Count} entries [{measureEligibilityLabel}] | scenario={scenario.PrimaryDxDisplay} | " +
                         $"encounter={encounterId} ({encStart:yyyy-MM-dd} ? {encEnd:yyyy-MM-dd})");

        // Record patient in manifest builder
        manifestBuilder.AddPatient(patientId, profile);
        manifestBuilder.AddEntries(patientId, entries);

        // Compute per-resource CQL SDE filter exclusions from the actual generated resources
        // (inspects in-memory Encounter + Condition attributes — no seed replay).
        //
        // Filter rules apply only for measures this patient qualifies for. A non-qualifying
        // measure's MeasureReport does not contain the patient's resources, so its SDE
        // semantics do not contribute to the intersection of exclusions that determines
        // whether a resource reaches ABS.
        var cqlInput = CqlFilterInputExtractor.ExtractFromEntries(patientId, entries);
        if (cqlInput != null)
        {
            var qualifyingMeasures = measures.Where(profile.QualifiesFor).ToList();
            if (qualifyingMeasures.Count > 0)
            {
                var cqlFilteredKeys = CqlFilterSimulator.ComputeFilteredKeys(qualifyingMeasures, cqlInput);
                manifestBuilder.SetCqlFilteredKeys(patientId, cqlFilteredKeys);
            }
        }

        // Run acquisition simulation BEFORE we serialize and discard
        if (acquisitionSimulation != null)
        {
            var patientSimEntries = BuildResourceIndex(entries);
            var acquiredKeys = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
                patientId,
                patientSimEntries,
                sharedSimEntries,
                acquisitionSimulation.QueryPlan,
                acquisitionSimulation.ClinicalPeriodStart,
                acquisitionSimulation.ClinicalPeriodEnd);
            manifestBuilder.SetSimulatedAcquiredKeys(patientId, acquiredKeys);
            // patientSimEntries (JsonElement clones) are now eligible for GC
        }

        // Serialize into chunks, upload sequentially, then discard
        var bundles = ChunkEntries(entries, patientId, 0);

        // Entries list is no longer needed — allow GC before upload
        entries.Clear();

        var progress = $"[{patientId}] ";
        await fhirDataLoader.UploadBundlesSequentiallyAsync(output, bundles, progress);

        var bundleCount = bundles.Count;

        // Bundles are no longer needed — allow GC
        bundles.Clear();

        return (patientId, profile, bundleCount);
    }

    /// <summary>
    /// Generates all FHIR bundle entries for a single patient using the same logic as
    /// Builds a single patient's FHIR bundle entries (profile-driven scenario + measure eligibility).
    /// </summary>
    /// <param name="generationClinicalPeriodStart">
    /// Optional clinical-period start. When provided together with <paramref name="generationClinicalPeriodEnd"/>,
    /// the encounter window is bound inside the period so resources spread across the encounter LOS
    /// remain inside any downstream consumer's date filter. Null falls back to the seed-only encounter scheme.
    /// </param>
    /// <param name="generationClinicalPeriodEnd">Companion to <paramref name="generationClinicalPeriodStart"/>.</param>
    private static List<Bundle.EntryComponent> GeneratePatientEntries(
        PatientProfile profile,
        int patientIndex,
        int baseSeed,
        int totalResourcesPerPatient,
        string patientIdPrefix,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        IReadOnlyList<ProfiledMeasureType> measures,
        DateTime? generationClinicalPeriodStart,
        DateTime? generationClinicalPeriodEnd,
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids)
    {
        var patientSeed = baseSeed + (profile.SeedOffset ?? patientIndex);
        var patientId = ids.PatientId(patientIndex);
        var scenario = FhirGenerationCodes.GetScenarioById(profile.ClinicalScenarioId)
                       ?? FhirGenerationCodes.GetScenarioBySeed(patientSeed);
        var anchors = ScenarioResourceGeneration.ComputePatientAnchors(patientId, patientSeed, sharedPractitionerIds);

        // Same helper used by the post-generation manifest log block, so the two
        // computations can never drift. When no period is provided the helpers
        // fall back to the legacy seed-only schemes so existing callers keep
        // their stable 2023-anchored (inpatient) and 2020-anchored (outpatient) dates.
        DateTime encStart, encEnd;
        if (profile.RequiresInpatientEncounter())
        {
            (encStart, encEnd) = FhirBundleGenerator.DeriveInpatientEncounterWindow(
                patientSeed, generationClinicalPeriodStart, generationClinicalPeriodEnd);
        }
        else
        {
            (encStart, encEnd) = FhirBundleGenerator.DeriveOutpatientEncounterWindow(
                patientSeed, generationClinicalPeriodStart, generationClinicalPeriodEnd);
        }

        var entries = new List<Bundle.EntryComponent>();

        Resource encounter;
        if (profile.RequiresInpatientEncounter())
        {
            if (profile.RequiresHypoglycemicMedication())
            {
                encounter = EncounterFactory.Create(
                    anchors.EncounterId, patientId, encStart, encEnd,
                    anchors.AttendingPractId, anchors.AdmittingPractId,
                    ids.EdLocation, ids.IcuLocation,
                    ids.StepDownLocation, ids.Organization,
                    anchors.PrimaryDxId,
                    "32485007", "Hospital admission (procedure)",
                    scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd,
                    scenario.AdmitSourceCode, scenario.AdmitSourceDisplay,
                    scenario.DischargeDispositionCode, scenario.DischargeDispositionDisplay,
                    scenario.ServiceTypeCode, scenario.ServiceTypeDisplay,
                    "EM", "emergency");
            }
            else
            {
                encounter = EncounterFactory.Generate(
                    anchors.EncounterId, patientId, encStart, encEnd,
                    anchors.AttendingPractId, anchors.AdmittingPractId,
                    ids.EdLocation, ids.IcuLocation,
                    ids.StepDownLocation, ids.Organization,
                    anchors.PrimaryDxId, scenario);
            }
        }
        else
        {
            encounter = EncounterFactory.CreateAmbulatory(
                anchors.EncounterId, patientId, encStart, encEnd,
                anchors.AttendingPractId, ids.OutpatientLocation,
                ids.Organization,
                anchors.PrimaryDxId,
                scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd);
        }

        ScenarioResourceGeneration.AddPatientCoreAndScenarioResources(
            entries, patientId, patientSeed, patientIndex, baseSeed, totalResourcesPerPatient,
            patientIdPrefix, encStart, encEnd, scenario, anchors, encounter,
            sharedPractitionerIds, sharedMedicationIds, config, ids,
            addHypoglycemicMedicationPair: profile.RequiresHypoglycemicMedication());

        return entries;
    }

    // ------------------------------------------------------------------
    //  Shared infrastructure generation
    // ------------------------------------------------------------------

    private static (List<Bundle.EntryComponent> Entries, List<string> PractitionerIds, List<string> MedicationIds, FhirBundleGenerator.SharedIds Ids)
        GenerateSharedInfrastructure(string patientIdPrefix)
    {
        // All shared-infrastructure construction lives in ScenarioResourceGeneration so
        // FhirBundleGenerator (bulk path) and FhirGenerationPipeline (streaming path)
        // can never drift on shared-resource shape, IDs, or order.
        var ids = new FhirBundleGenerator.SharedIds(patientIdPrefix);
        var (entries, practitionerIds, medicationIds) = ScenarioResourceGeneration.BuildSharedInfrastructure(ids);
        return (entries, practitionerIds, medicationIds, ids);
    }

    // ------------------------------------------------------------------
    //  Chunking and serialization
    // ------------------------------------------------------------------

    private static List<(string Name, string Json)> ChunkEntries(
        List<Bundle.EntryComponent> entries,
        string contextId,
        int startChunkIndex)
    {
        var bundles = new List<(string Name, string Json)>();
        var chunkIndex = startChunkIndex;

        for (var i = 0; i < entries.Count; i += MaxEntriesPerBundle)
        {
            chunkIndex++;
            var chunkSize = Math.Min(MaxEntriesPerBundle, entries.Count - i);
            var chunk = entries.GetRange(i, chunkSize);
            bundles.Add(($"{contextId}_chunk{chunkIndex:D2}", ScenarioResourceGeneration.Serialize(chunk)));
        }

        return bundles;
    }

    /// <summary>
    /// Converts in-memory FHIR bundle entries into (ResourceType, ResourceId, Key, JsonElement)
    /// tuples for use by <see cref="QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient"/>.
    /// Each entry is serialized individually and parsed to produce a <see cref="JsonElement"/>
    /// that the simulator can inspect for category, date, and reference properties.
    /// </summary>
    private static List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> BuildResourceIndex(
        IReadOnlyList<Bundle.EntryComponent> entries)
    {
        var result = new List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>(entries.Count);
        var serializerOptions = FhirSerializerOptions.ForFhirWithoutValidation();

        foreach (var entry in entries)
        {
            var url = entry.Request?.Url;
            if (string.IsNullOrWhiteSpace(url) || !url.Contains('/'))
                continue;

            var slashIdx = url.IndexOf('/');
            var resourceType = url[..slashIdx];
            var resourceId = url[(slashIdx + 1)..];

            if (entry.Resource == null)
                continue;

            // Serialize the individual resource to JSON and parse to JsonElement
            var json = JsonSerializer.Serialize(entry.Resource, entry.Resource.GetType(), serializerOptions);
            using var doc = JsonDocument.Parse(json);
            result.Add((resourceType, resourceId, url, doc.RootElement.Clone()));
        }

        return result;
    }
}
