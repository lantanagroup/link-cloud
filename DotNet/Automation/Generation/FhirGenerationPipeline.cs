using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation.ResourceFactories;
using LantanaGroup.Automation.Helpers;
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
        public string? ReportStart { get; init; }
        public string? ReportEnd { get; init; }
    }

    /// <summary>
    /// Generates patients with explicit profiles, uploads each patient's data to the FHIR
    /// server as it's generated, and returns a manifest with all metadata needed for validation.
    /// No serialized FHIR JSON is retained after this method returns.
    /// </summary>
    public static async Task<PipelineResult> GenerateAndUploadAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = FhirBundleGenerator.DefaultResourcesPerPatient,
        string patientIdPrefix = "ProfilePatient",
        int? generationSeed = null,
        FhirGenerationConfig? config = null,
        AcquisitionSimulationConfig? acquisitionSimulation = null)
    {
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));
        if (profiles == null || profiles.Count == 0)
            throw new ArgumentException("At least one patient profile is required.", nameof(profiles));

        var baseSeed = generationSeed.GetValueOrDefault();
        var manifestBuilder = new GenerationManifest.IncrementalBuilder();

        var qualifyingAllCount = profiles.Count(p => p.QualifiesForAll(measures));
        var nonQualifyingAllCount = profiles.Count(p => p.QualifiesForNone(measures));
        var mixedEligibilityCount = profiles.Count - qualifyingAllCount - nonQualifyingAllCount;
        output.WriteLine($"[Pipeline] Generating {profiles.Count} profiled patients ({qualifyingAllCount} qualifying-all, " +
                         $"{nonQualifyingAllCount} non-qualifying-all, {mixedEligibilityCount} mixed) " +
                         $"with ~{totalResourcesPerPatient} resources each..." +
                         (generationSeed.HasValue ? $" (seed={generationSeed.Value})" : string.Empty));

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
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids)
    {
        var patientSeed = baseSeed + (profile.SeedOffset ?? patientIndex);
        var patientId = ids.PatientId(patientIndex);

        // Generate entries using the same logic as FhirBundleGenerator.GenerateWithProfilesCore
        var entries = GeneratePatientEntries(
            profile, patientIndex, baseSeed, totalResourcesPerPatient, patientIdPrefix,
            sharedPractitionerIds, sharedMedicationIds, measures, config, ids);

        var scenario = FhirGenerationCodes.GetScenarioById(profile.ClinicalScenarioId)
                       ?? FhirGenerationCodes.GetScenarioBySeed(patientSeed);

        DateTime encStart, encEnd;
        if (profile.RequiresInpatientEncounter())
        {
            encStart = EncounterStart(patientSeed);
            encEnd = EncounterEnd(patientSeed);
        }
        else
        {
            encStart = new DateTime(2020, 1 + Mod(patientSeed, 6), 1 + Mod(patientSeed * 3, 28),
                                    8 + Mod(patientSeed, 4), 0, 0, DateTimeKind.Utc);
            encEnd = encStart.AddHours(2 + Mod(patientSeed, 4));
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

        // Compute per-resource CQL SDE filter exclusions with measure-family profiles
        var scenarioIdxForFilter = FhirGenerationCodes.GetScenarioArrayPosition(scenario);
        var cqlFilteredKeys = CqlFilterSimulator.ComputeFilteredKeys(
            measures,
            patientId,
            encounterId,
            encStart,
            encEnd,
            scenarioIdxForFilter,
            baseSeed,
            patientIndex,
            totalResourcesPerPatient,
            config);
        manifestBuilder.SetCqlFilteredKeys(patientId, cqlFilteredKeys);

        // Run acquisition simulation BEFORE we serialize and discard
        if (acquisitionSimulation != null)
        {
            var patientSimEntries = BuildResourceIndex(entries);
            var acquiredKeys = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
                patientId,
                patientSimEntries,
                sharedSimEntries,
                acquisitionSimulation.QueryPlan,
                acquisitionSimulation.ReportStart,
                acquisitionSimulation.ReportEnd);
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
    /// <see cref="FhirBundleGenerator.GenerateWithProfilesCore"/>.
    /// </summary>
    private static List<Bundle.EntryComponent> GeneratePatientEntries(
        PatientProfile profile,
        int patientIndex,
        int baseSeed,
        int totalResourcesPerPatient,
        string patientIdPrefix,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        IReadOnlyList<ProfiledMeasureType> measures,
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids)
    {
        var patientSeed = baseSeed + (profile.SeedOffset ?? patientIndex);
        var patientId = ids.PatientId(patientIndex);
        var scenario = FhirGenerationCodes.GetScenarioById(profile.ClinicalScenarioId)
                       ?? FhirGenerationCodes.GetScenarioBySeed(patientSeed);
        var scenarioIdx = FhirGenerationCodes.GetScenarioArrayPosition(scenario);
        var attendingPractId = sharedPractitionerIds[Mod(patientSeed, sharedPractitionerIds.Count)];
        var admittingPractId = sharedPractitionerIds[Mod(patientSeed + 1, sharedPractitionerIds.Count)];
        var gpPractId = sharedPractitionerIds[Mod(patientSeed + 2, sharedPractitionerIds.Count)];
        var encounterId = $"{patientId}-Enc-001";
        var careTeamId = $"{patientId}-CareTeam-001";
        var carePlanId = $"{patientId}-CarePlan-001";
        var patientDeviceId = $"{patientId}-Device-001";
        var primaryDxId = $"{patientId}-Condition-primary";

        DateTime encStart, encEnd;
        if (profile.RequiresInpatientEncounter())
        {
            encStart = EncounterStart(patientSeed);
            encEnd = EncounterEnd(patientSeed);
        }
        else
        {
            encStart = new DateTime(2020, 1 + Mod(patientSeed, 6), 1 + Mod(patientSeed * 3, 28),
                                    8 + Mod(patientSeed, 4), 0, 0, DateTimeKind.Utc);
            encEnd = encStart.AddHours(2 + Mod(patientSeed, 4));
        }

        var entries = new List<Bundle.EntryComponent>();

        var patient = PatientFactory.Generate(patientId, patientSeed, gpPractId);
        patient.ManagingOrganization = new ResourceReference($"Organization/{ids.Organization}", "General Test Hospital");
        entries.Add(Entry($"Patient/{patientId}", patient));

        entries.Add(Entry($"Device/{patientDeviceId}",
            DeviceFactory.Generate(patientDeviceId, patientSeed, patientId)));

        entries.Add(Entry($"Condition/{primaryDxId}",
            ConditionFactory.CreatePrimary(
                primaryDxId, patientId, encounterId, encStart,
                scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));

        if (profile.RequiresInpatientEncounter())
        {
            if (profile.RequiresHypoglycemicMedication())
            {
                entries.Add(Entry($"Encounter/{encounterId}",
                    EncounterFactory.Create(
                        encounterId, patientId, encStart, encEnd,
                        attendingPractId, admittingPractId,
                        ids.EdLocation, ids.IcuLocation,
                        ids.StepDownLocation, ids.Organization,
                        primaryDxId,
                        "32485007", "Hospital admission (procedure)",
                        scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd,
                        scenario.AdmitSourceCode, scenario.AdmitSourceDisplay,
                        scenario.DischargeDispositionCode, scenario.DischargeDispositionDisplay,
                        scenario.ServiceTypeCode, scenario.ServiceTypeDisplay,
                        "EM", "emergency")));
            }
            else
            {
                entries.Add(Entry($"Encounter/{encounterId}",
                    EncounterFactory.Generate(
                        encounterId, patientId, encStart, encEnd,
                        attendingPractId, admittingPractId,
                        ids.EdLocation, ids.IcuLocation,
                        ids.StepDownLocation, ids.Organization,
                        primaryDxId, scenario)));
            }
        }
        else
        {
            entries.Add(Entry($"Encounter/{encounterId}",
                EncounterFactory.CreateAmbulatory(
                    encounterId, patientId, encStart, encEnd,
                    attendingPractId, ids.OutpatientLocation,
                    ids.Organization,
                    primaryDxId,
                    scenario.PrimaryDxSnomed, scenario.PrimaryDxDisplay, scenario.PrimaryDxIcd)));
        }

        entries.Add(Entry($"CareTeam/{careTeamId}",
            CareTeamFactory.Generate(careTeamId, patientId, encounterId, attendingPractId, encStart, ids.Organization)));

        entries.Add(Entry($"CarePlan/{carePlanId}",
            CarePlanFactory.Generate(carePlanId, patientId, encounterId, careTeamId, encStart, patientSeed)));

        if (profile.RequiresHypoglycemicMedication())
        {
            AddHypoglycemicQualifyingMedicationEntries(entries, patientId, encounterId, attendingPractId, patientSeed, encStart, ids);
        }

        GenerateScenarioDrivenResources(entries, scenarioIdx, patientId, encounterId,
            encStart, encEnd, primaryDxId, attendingPractId, careTeamId, patientIdPrefix,
            totalResourcesPerPatient, baseSeed, patientIndex, sharedPractitionerIds, sharedMedicationIds, config, ids);

        return entries;
    }

    // ------------------------------------------------------------------
    //  Shared infrastructure generation
    // ------------------------------------------------------------------

    private static (List<Bundle.EntryComponent> Entries, List<string> PractitionerIds, List<string> MedicationIds, FhirBundleGenerator.SharedIds Ids)
        GenerateSharedInfrastructure(string patientIdPrefix)
    {
        var ids = new FhirBundleGenerator.SharedIds(patientIdPrefix);
        var entries = new List<Bundle.EntryComponent>
        {
            Entry($"Organization/{ids.Organization}", OrganizationFactory.Generate(ids.Organization)),
            Entry($"Location/{ids.HospitalLocation}", LocationFactory.Generate(ids.HospitalLocation, "HOSP", "Main Hospital", ids.Organization)),
            Entry($"Location/{ids.IcuLocation}", LocationFactory.Generate(ids.IcuLocation, "ICU", "Intensive Care Unit", ids.Organization)),
            Entry($"Location/{ids.EdLocation}", LocationFactory.Generate(ids.EdLocation, "ER", "Emergency Department", ids.Organization)),
            Entry($"Location/{ids.StepDownLocation}", LocationFactory.Generate(ids.StepDownLocation, "HU", "Step-Down Unit", ids.Organization)),
            Entry($"Location/{ids.OutpatientLocation}", LocationFactory.Create(ids.OutpatientLocation, "OF", "Outpatient Clinic", ids.Organization)),
            Entry($"Device/{ids.DevicePulseOx}", DeviceFactory.Create(ids.DevicePulseOx, "706689003", "Pulse oximeter", null)),
            Entry($"Device/{ids.DeviceVentilator}", DeviceFactory.Create(ids.DeviceVentilator, "706172005", "Ventilator", null)),
            Entry($"Device/{ids.DeviceCPAP}", DeviceFactory.Create(ids.DeviceCPAP, "10776007", "Continuous positive airway pressure device", null)),
        };

        var practitionerIds = new List<string>();
        for (var pi = 0; pi < FhirGenerationCodes.Practitioners.Length; pi++)
        {
            var practId = ids.PractitionerId(pi);
            practitionerIds.Add(practId);
            entries.Add(Entry($"Practitioner/{practId}", PractitionerFactory.Generate(practId, pi)));
        }

        var medicationIds = GenerateSharedMedications(entries, ids);

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
            bundles.Add(($"{contextId}_chunk{chunkIndex:D2}", Serialize(chunk)));
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

    // ------------------------------------------------------------------
    //  Helpers — delegated from FhirBundleGenerator (same logic)
    // ------------------------------------------------------------------

    private static Bundle.EntryComponent Entry(string resourceUrl, Resource resource) => new()
    {
        FullUrl = $"http://localhost:8080/fhir/{resourceUrl}",
        Resource = resource,
        Request = new Bundle.RequestComponent { Method = Bundle.HTTPVerb.PUT, Url = resourceUrl }
    };

    private static string Serialize(List<Bundle.EntryComponent> entries)
    {
        var bundle = new Bundle { Type = Bundle.BundleType.Transaction, Entry = entries };
        return JsonSerializer.Serialize(bundle, FhirSerializerOptions.ForFhirWithoutValidation());
    }

    private static DateTime EncounterStart(int index)
    {
        const int baseYear = 2023;
        const int baseMonth = 1;
        var monthOffset = index % 12;
        var dayOffset = (index * 3) % 28;
        var month = ((baseMonth - 1 + monthOffset) % 12) + 1;
        var year = baseYear + (baseMonth - 1 + monthOffset) / 12;
        return new DateTime(year, month, 1 + dayOffset, 6 + (index % 6), 0, 0, DateTimeKind.Utc);
    }

    private static DateTime EncounterEnd(int index) =>
        EncounterStart(index).AddDays(2 + ((index * 7) % 20)).AddHours(4);

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    // ------------------------------------------------------------------
    //  Scenario-driven resource generation (same as FhirBundleGenerator)
    // ------------------------------------------------------------------

    private static (string ResourceType, double Fraction)[] ResolveDistribution(FhirGenerationConfig? config)
    {
        var dict = (config ?? new FhirGenerationConfig()).ResourceDistribution;
        return dict.Select(kv => (kv.Key, kv.Value)).ToArray();
    }

    private static void GenerateScenarioDrivenResources(
        List<Bundle.EntryComponent> entries,
        int scenarioIdx,
        string patientId,
        string encounterId,
        DateTime encStart,
        DateTime encEnd,
        string primaryDxId,
        string attendingPractId,
        string careTeamId,
        string patientIdPrefix,
        int totalResourcesPerPatient,
        int baseSeed,
        int patientOrdinal,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        FhirGenerationConfig? config,
        FhirBundleGenerator.SharedIds ids)
    {
        var medIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalMedicationIndices, ScenarioResourceMap.ScenarioMedicationIndices,
            scenarioIdx, FhirGenerationCodes.Medications.Length);
        var obsIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalObservationIndices, ScenarioResourceMap.ScenarioObservationIndices,
            scenarioIdx, FhirGenerationCodes.Observations.Length);
        var procIndices = ScenarioResourceMap.ScenarioProcedureIndices[
            Mod(scenarioIdx, ScenarioResourceMap.ScenarioProcedureIndices.Length)];
        var specIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalSpecimenIndices, ScenarioResourceMap.ScenarioSpecimenIndices,
            scenarioIdx, FhirGenerationCodes.Specimens.Length);
        var imgIndices = ScenarioResourceMap.ScenarioImagingIndices[
            Mod(scenarioIdx, ScenarioResourceMap.ScenarioImagingIndices.Length)];
        var srIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalServiceRequestIndices, ScenarioResourceMap.ScenarioServiceRequestIndices,
            scenarioIdx, FhirGenerationCodes.ServiceRequests.Length);
        var condIndices = ScenarioResourceMap.GetMergedIndices(
            ScenarioResourceMap.UniversalConditionIndices, ScenarioResourceMap.ScenarioConditionIndices,
            scenarioIdx, FhirGenerationCodes.Conditions.Length);

        var medicationRequestIds = new List<string>();
        var specimenIds = new List<string>();
        var observationIds = new List<string>();
        var conditionIds = new List<string> { primaryDxId };
        var serviceRequestIds = new List<string>();
        var diagnosticReportIds = new List<string>();
        var resourceIndex = 0;
        var distribution = ResolveDistribution(config);
        var includeLowValueOptionalReferences = config?.IncludeLowValueOptionalReferences ?? true;

        foreach (var (resourceType, fraction) in distribution)
        {
            var count = Math.Max(1, (int)(totalResourcesPerPatient * fraction));

            for (var i = 0; i < count; i++)
            {
                resourceIndex++;
                var seed = baseSeed + (patientOrdinal * 31 + i);
                var resourceId = $"{patientId}-{FhirBundleGenerator.AbbreviateResourceType(resourceType)}-{resourceIndex:D3}";
                var offset = TimeSpan.FromMinutes((double)i / Math.Max(count, 1) * (encEnd - encStart).TotalMinutes);
                var effectiveDate = encStart.Add(offset);
                var practId = sharedPractitionerIds[Mod(seed, sharedPractitionerIds.Count)];

                Resource resource = resourceType switch
                {
                    "Observation" => GenerateScenarioObservation(resourceId, patientId, encounterId, effectiveDate, seed, obsIndices, specimenIds, observationIds, ids),
                    "Condition" => GenerateScenarioCondition(resourceId, patientId, encounterId, effectiveDate, encEnd, seed, condIndices, conditionIds),
                    "Procedure" => GenerateScenarioProcedure(resourceId, patientId, encounterId, effectiveDate, seed, practId, procIndices, conditionIds, ids),
                    "MedicationRequest" => GenerateScenarioMedicationRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, medIndices, conditionIds, sharedMedicationIds, medicationRequestIds),
                    "MedicationAdministration" => GenerateScenarioMedicationAdministration(resourceId, patientId, encounterId, effectiveDate, seed, medIndices, sharedMedicationIds, medicationRequestIds, practId, includeLowValueOptionalReferences),
                    "DiagnosticReport" => GenerateScenarioDiagnosticReport(resourceId, patientId, encounterId, effectiveDate, seed, observationIds, specimenIds, practId, diagnosticReportIds),
                    "ServiceRequest" => GenerateScenarioServiceRequest(resourceId, patientId, encounterId, effectiveDate, seed, practId, srIndices, conditionIds, serviceRequestIds, ids),
                    "Coverage" => CoverageFactory.Generate(resourceId, patientId, encStart, encEnd, seed),
                    "Specimen" => GenerateScenarioSpecimen(resourceId, patientId, effectiveDate, seed, specIndices, specimenIds, practId),
                    "AllergyIntolerance" => AllergyIntoleranceFactory.Generate(resourceId, patientId, encStart, seed, practId),
                    "Immunization" => ImmunizationFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, ids.HospitalLocation, ids.Organization),
                    "ImagingStudy" => GenerateScenarioImagingStudy(resourceId, patientId, encounterId, effectiveDate, seed, imgIndices, serviceRequestIds, practId, includeLowValueOptionalReferences, ids),
                    "CareTeam" => CareTeamFactory.Generate(resourceId, patientId, encounterId, attendingPractId, effectiveDate, ids.Organization),
                    "CarePlan" => CarePlanFactory.Generate(resourceId, patientId, encounterId, careTeamId, effectiveDate, seed),
                    "DocumentReference" => DocumentReferenceFactory.Generate(resourceId, patientId, encounterId, effectiveDate, seed, ids.Organization, attendingPractId),
                    "Provenance" => GenerateScenarioProvenance(resourceId, patientId, encounterId, effectiveDate, practId, diagnosticReportIds, includeLowValueOptionalReferences, ids),
                    _ => throw new InvalidOperationException($"Unknown resource type: {resourceType}")
                };

                entries.Add(Entry($"{resourceType}/{resourceId}", resource));
            }
        }

        var listId = $"SyntheticList-{patientId}";
        entries.Add(Entry($"List/{listId}",
            CensusListFactory.Generate(listId, patientId, patientIdPrefix, encStart)));
    }

    // ------------------------------------------------------------------
    //  Scenario-aware resource generators (same as FhirBundleGenerator)
    // ------------------------------------------------------------------

    private static Observation GenerateScenarioObservation(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] obsIndices, List<string> specimenIds, List<string> observationIds, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(obsIndices, seed, FhirGenerationCodes.Observations.Length);
        var v = FhirGenerationCodes.Observations[poolIdx];
        observationIds.Add(id);
        return ObservationFactory.Create(id, patientId, encounterId, effective,
            v.Code, v.Display, v.Category, v.Unit,
            v.CritLow, v.NormLow, v.NormHigh, v.CritHigh, seed, specimenIds, ids.Organization);
    }

    private static Condition GenerateScenarioCondition(
        string id, string patientId, string encounterId, DateTime onset, DateTime abatement, int seed,
        int[] condIndices, List<string> conditionIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(condIndices, seed, FhirGenerationCodes.Conditions.Length);
        var v = FhirGenerationCodes.Conditions[poolIdx];
        conditionIds.Add(id);
        return ConditionFactory.Create(id, patientId, encounterId, onset, abatement, seed,
            v.Code, v.Display, v.IcdCode, v.Category);
    }

    private static Procedure GenerateScenarioProcedure(
        string id, string patientId, string encounterId, DateTime performed, int seed, string practId,
        int[] procIndices, List<string> conditionIds, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(procIndices, seed, FhirGenerationCodes.Procedures.Length);
        var v = FhirGenerationCodes.Procedures[poolIdx];
        return ProcedureFactory.Create(id, patientId, encounterId, performed, seed, practId,
            ids.HospitalLocation, ids.Organization,
            v.Code, v.Display, v.BodySiteCode, v.BodySiteDisplay,
            v.OutcomeCode, v.OutcomeDisplay,
            conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null);
    }

    private static MedicationRequest GenerateScenarioMedicationRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] medIndices, List<string> conditionIds, List<string> sharedMedicationIds, List<string> medicationRequestIds)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        var medicationRefId = poolIdx < sharedMedicationIds.Count ? sharedMedicationIds[poolIdx] : null;
        var req = MedicationRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.FreqPerDay, v.Prn,
            v.IndicationSnomed, v.IndicationDisplay, reasonConditionId, medicationRefId);
        medicationRequestIds.Add(id);
        return req;
    }

    private static MedicationAdministration GenerateScenarioMedicationAdministration(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        int[] medIndices, List<string> sharedMedicationIds, List<string> medicationRequestIds, string practId,
        bool includeLowValueOptionalReferences)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(medIndices, seed, FhirGenerationCodes.Medications.Length);
        var v = FhirGenerationCodes.Medications[poolIdx];
        var medRefId = poolIdx < sharedMedicationIds.Count ? sharedMedicationIds[poolIdx] : null;
        var isIv = v.RouteCode == "47625008";
        var admin = MedicationAdministrationFactory.Create(id, patientId, encounterId, effective, seed, practId,
            v.RxCode, v.Display, v.RouteCode, v.RouteDisplay,
            v.DoseValue, v.DoseUnit, v.IndicationSnomed, v.IndicationDisplay, isIv, medRefId);
        if (includeLowValueOptionalReferences && medicationRequestIds.Count > 0)
            admin.Request = new ResourceReference($"MedicationRequest/{medicationRequestIds[seed % medicationRequestIds.Count]}");
        return admin;
    }

    private static Specimen GenerateScenarioSpecimen(
        string id, string patientId, DateTime collected, int seed,
        int[] specIndices, List<string> specimenIds, string practId)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(specIndices, seed, FhirGenerationCodes.Specimens.Length);
        specimenIds.Add(id);
        var v = FhirGenerationCodes.Specimens[poolIdx];
        return SpecimenFactory.Create(id, patientId, collected, seed,
            v.TypeCode, v.TypeDisplay, v.TypeSystem,
            v.ContainerCode, v.ContainerDisplay,
            v.CollectionMethod, v.BodySiteCode, v.BodySiteDisplay, practId);
    }

    private static DiagnosticReport GenerateScenarioDiagnosticReport(
        string id, string patientId, string encounterId, DateTime effective, int seed,
        List<string> observationIds, List<string> specimenIds, string practId,
        List<string> diagnosticReportIds)
    {
        var report = DiagnosticReportFactory.Generate(id, patientId, encounterId, effective, seed,
            observationIds, specimenIds, practId);
        diagnosticReportIds.Add(id);
        return report;
    }

    private static ServiceRequest GenerateScenarioServiceRequest(
        string id, string patientId, string encounterId, DateTime authored, int seed, string practId,
        int[] srIndices, List<string> conditionIds, List<string> serviceRequestIds, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(srIndices, seed, FhirGenerationCodes.ServiceRequests.Length);
        var v = FhirGenerationCodes.ServiceRequests[poolIdx];
        var reasonConditionId = conditionIds.Count > 0 ? conditionIds[seed % conditionIds.Count] : null;
        var sr = ServiceRequestFactory.Create(id, patientId, encounterId, authored, seed, practId,
            v.Code, v.Display, v.IsLab, v.System, reasonConditionId, ids.Organization);
        serviceRequestIds.Add(id);
        return sr;
    }

    private static ImagingStudy GenerateScenarioImagingStudy(
        string id, string patientId, string encounterId, DateTime started, int seed,
        int[] imgIndices, List<string> serviceRequestIds, string practId,
        bool includeLowValueOptionalReferences, FhirBundleGenerator.SharedIds ids)
    {
        var poolIdx = ScenarioResourceMap.PickIndex(imgIndices, seed, FhirGenerationCodes.ImagingStudies.Length);
        var v = FhirGenerationCodes.ImagingStudies[poolIdx];
        var study = ImagingStudyFactory.Create(id, patientId, encounterId, started, ids.HospitalLocation, practId,
            v.SnomedCode, v.Display, v.Modality,
            v.BodySiteCode, v.BodySiteDisplay, v.ReasonCode, v.ReasonDisplay);
        if (includeLowValueOptionalReferences && serviceRequestIds.Count > 0)
        {
            study.BasedOn ??= [];
            study.BasedOn.Add(new ResourceReference($"ServiceRequest/{serviceRequestIds[seed % serviceRequestIds.Count]}"));
        }
        return study;
    }

    private static Provenance GenerateScenarioProvenance(
        string id, string patientId, string encounterId, DateTime recorded, string practId,
        List<string> diagnosticReportIds,
        bool includeLowValueOptionalReferences, FhirBundleGenerator.SharedIds ids)
    {
        var prov = ProvenanceFactory.Create(id, patientId, encounterId, recorded, practId, ids.Organization);
        if (includeLowValueOptionalReferences && diagnosticReportIds.Count > 0)
        {
            prov.Target ??= [];
            prov.Target.Add(new ResourceReference($"DiagnosticReport/{diagnosticReportIds[^1]}"));
        }
        return prov;
    }

    private static void AddHypoglycemicQualifyingMedicationEntries(
        List<Bundle.EntryComponent> entries,
        string patientId,
        string encounterId,
        string practitionerId,
        int seed,
        DateTime encounterStart,
        FhirBundleGenerator.SharedIds ids)
    {
        const string insulinRxNorm = "274783";
        const string insulinDisplay = "insulin glargine";
        const string subcutaneousRouteCode = "34206005";
        const string subcutaneousRouteDisplay = "Subcutaneous route";
        const string diabetesIndicationCode = "44054006";
        const string diabetesIndicationDisplay = "Diabetes mellitus type 2";

        var medicationRequestId = $"{patientId}-MedReq-A01";
        var medicationAdministrationId = $"{patientId}-MedAdm-A01";
        var medicationTime = encounterStart.AddHours(1);

        entries.Add(Entry($"MedicationRequest/{medicationRequestId}",
            MedicationRequestFactory.Create(
                medicationRequestId, patientId, encounterId, medicationTime, seed, practitionerId,
                insulinRxNorm, insulinDisplay, subcutaneousRouteCode, subcutaneousRouteDisplay,
                20, "[iU]", 1, false,
                diabetesIndicationCode, diabetesIndicationDisplay,
                null, ids.HypoInsulinGlargineMedication)));

        entries.Add(Entry($"MedicationAdministration/{medicationAdministrationId}",
            MedicationAdministrationFactory.Create(
                medicationAdministrationId, patientId, encounterId, medicationTime, seed, practitionerId,
                insulinRxNorm, insulinDisplay, subcutaneousRouteCode, subcutaneousRouteDisplay,
                20, "[iU]",
                diabetesIndicationCode, diabetesIndicationDisplay,
                false, ids.HypoInsulinGlargineMedication)));
    }

    private static List<string> GenerateSharedMedications(List<Bundle.EntryComponent> sharedEntries, FhirBundleGenerator.SharedIds sharedIds)
    {
        var medIds = new List<string>(FhirGenerationCodes.Medications.Length + 1);
        for (var i = 0; i < FhirGenerationCodes.Medications.Length; i++)
        {
            var v = FhirGenerationCodes.Medications[i];
            var medId = sharedIds.MedicationId(i);
            medIds.Add(medId);
            sharedEntries.Add(Entry($"Medication/{medId}",
                MedicationFactory.Create(medId, v.RxCode, v.Display, v.DoseValue, v.DoseUnit, v.RouteCode, v.RouteDisplay)));
        }

        sharedEntries.Add(Entry($"Medication/{sharedIds.HypoInsulinGlargineMedication}",
            MedicationFactory.Create(sharedIds.HypoInsulinGlargineMedication, "274783", "insulin glargine",
                20, "[iU]", "34206005", "Subcutaneous route")));

        return medIds;
    }
}
