using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Automation.Generation.Thetis;
using LantanaGroup.Automation.Helpers;
using System.Reflection;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// A streaming generation-and-upload pipeline that processes one patient at a time to
/// prevent OOM conditions with large patient counts. Each patient's FHIR data is:
///   1. Generated in-memory by Thetis Engine (plus Automation fixture overlays)
///   2. Manifest metadata accumulated from the in-memory objects
///   3. Serialized into transaction bundle chunks
///   4. Uploaded sequentially to the FHIR server (preserving resource dependency order)
///   5. Disposed — no serialized JSON or FHIR objects retained after upload
///
/// Multiple patients are processed concurrently (bounded by a configurable
/// max-concurrency value, defaulting to <see cref="DefaultMaxConcurrentPatients"/>)
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
    private const int DefaultMaxConcurrentPatients = 4;
    private const int VerbosePatientLogHeadCount = 5;
    private const int VerbosePatientLogInterval = 250;
    private const string TemplateRunTag = "template-run";
    private static readonly Lazy<string> GeneratorDependencyFingerprint = new(ComputeGeneratorDependencyFingerprint);

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

        /// <summary>
        /// Deterministic template cache keys used for generated patients, ordered by
        /// generated patient ordinal. Used for immutable cache-version pinning.
        /// </summary>
        public IReadOnlyList<string> GeneratedTemplateKeys { get; init; } = [];
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
        public IReadOnlyList<string>? OrganizationLocationConditionFhirPaths { get; init; }

        /// <summary>
        /// When true, the simulator may use encounter-anchored fallback for date-mismatch
        /// cases (resource has a recognized date, but falls outside strict ge/le bounds).
        /// Scheduled workflows rely on this to mirror downstream acquisition behavior.
        /// </summary>
        public bool AllowEncounterAnchoredDateOverrideForOutOfRange { get; init; }
    }

    /// <summary>
    /// Generates patients with explicit profiles, uploads each patient's data to the FHIR
    /// server as it's generated, and returns a manifest with all metadata needed for validation.
    /// No serialized FHIR JSON is retained after this method returns.
    /// </summary>
    /// <param name="runId">
    /// Optional explicit run identifier used to scope every generated FHIR resource ID to this
    /// invocation. When <c>null</c> (default) a fresh short GUID is generated so concurrent runs
    /// (e.g. multiple tests queued rapidly from the UI) cannot collide on shared-infrastructure
    /// or per-patient FHIR resource IDs. Provide a stable value only when reproducing
    /// a specific run for debugging.
    /// </param>
    public static async Task<PipelineResult> GenerateAndUploadAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        IReadOnlyList<ProfiledMeasureType> measures,
        IReadOnlyList<PatientProfile> profiles,
        int totalResourcesPerPatient = FhirBundleGenerator.DefaultResourcesPerPatient,
        int? generationSeed = null,
        FhirGenerationConfig? config = null,
        GenerationRequirementsPlan? generationRequirementsPlan = null,
        AcquisitionSimulationConfig? acquisitionSimulation = null,
        string? runId = null,
        IReadOnlyList<ImportedPatientInput>? importedPatients = null,
        IGeneratedPatientTemplateCache? generatedTemplateCache = null,
        int? maxConcurrentPatients = null,
        IPatientEntryGenerator? patientEntryGenerator = null,
        ISharedInfrastructureGenerator? sharedInfrastructureGenerator = null,
        IReadOnlyList<string>? measureBundleJsons = null)
    {
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));
        if ((profiles == null || profiles.Count == 0) && (importedPatients == null || importedPatients.Count == 0))
            throw new ArgumentException("At least one patient profile or imported patient is required.", nameof(profiles));

        profiles ??= [];

        var effectiveMaxConcurrentPatients = maxConcurrentPatients.HasValue && maxConcurrentPatients.Value > 0
            ? maxConcurrentPatients.Value
            : DefaultMaxConcurrentPatients;

        // Guarantee per-run ID uniqueness so concurrent pipeline invocations never collide
        // on shared-infrastructure or per-patient FHIR resource IDs. Every resource generated
        // downstream (Organization, Location, Practitioner, Medication, Patient, Encounter,
        // Condition, Observation, ...) is scoped by SharedIds.RunTag, so uniqueness here
        // isolates the entire run.
        var effectiveRunId = string.IsNullOrWhiteSpace(runId)
            ? Guid.NewGuid().ToString("N")[..8]
            : runId!.Trim();

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
        var profileResourceOverrides = profiles.Count(p => p.ResourcesPerPatient.HasValue);
        var resourceShape = profileResourceOverrides > 0
            ? $"run default ~{totalResourcesPerPatient} resources (per-profile overrides: {profileResourceOverrides})"
            : $"~{totalResourcesPerPatient} resources each";
        output.WriteLine($"[Pipeline] Generating {profiles.Count} profiled patients ({qualifyingAllCount} qualifying-all, " +
                         $"{nonQualifyingAllCount} non-qualifying-all, {mixedEligibilityCount} mixed) " +
                         $"with {resourceShape}, runId='{effectiveRunId}'" +
                         (generationSeed.HasValue ? $", seed={generationSeed.Value}" : string.Empty) + "...");

        // ------------------------------------------------------------------
        // Shared infrastructure — generated once, uploaded first.
        // Uploaded even for imported-only runs: acquisition simulation and
        // org-location prediction treat these as run-scoped fixtures. Skipping
        // the POST would predict ABS keys that were never created.
        // ------------------------------------------------------------------
        var (sharedEntries, sharedPractitionerIds, sharedMedicationIds, ids) =
            GenerateSharedInfrastructure(generationRequirementsPlan, effectiveRunId, sharedInfrastructureGenerator);

        if (generatedTemplateCache != null && !IsSafeRunTagForTemplateCache(ids.RunTag))
        {
            output.WriteLine($"[Pipeline] Run tag '{ids.RunTag}' is not in the expected generated format; skipping generated template cache for this run.");
            generatedTemplateCache = null;
        }

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
        var generatedTemplateKeys = new string[profiles.Count];
        var totalBundlesUploaded = sharedBundles.Count;
        var completedPatients = 0;
        var semaphore = new SemaphoreSlim(effectiveMaxConcurrentPatients, effectiveMaxConcurrentPatients);

        var tasks = new System.Threading.Tasks.Task[profiles.Count];
        for (var p = 0; p < profiles.Count; p++)
        {
            var patientIndex = p; // capture for closure
            tasks[p] = System.Threading.Tasks.Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (patientId, profile, bundleCount, templateKey) = await GenerateAndUploadSinglePatientAsync(
                        output,
                        fhirDataLoader,
                        manifestBuilder,
                        profiles[patientIndex],
                        patientIndex,
                        baseSeed,
                        totalResourcesPerPatient,
                        measures,
                        sharedPractitionerIds,
                        sharedMedicationIds,
                        sharedSimEntries,
                        acquisitionSimulation,
                        generationClinicalPeriodStart,
                        generationClinicalPeriodEnd,
                        config,
                        generationRequirementsPlan,
                        ids,
                        generatedTemplateCache,
                        patientEntryGenerator,
                        measureBundleJsons);

                    patientIds[patientIndex] = patientId;
                    generatedTemplateKeys[patientIndex] = templateKey;
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

        // ------------------------------------------------------------------
        // Imported patients (sequential — bundles uploaded, IDs left alone)
        // ------------------------------------------------------------------
        var importedPatientIds = new List<string>();
        if (importedPatients is { Count: > 0 })
        {
            output.WriteLine($"[Pipeline] Processing {importedPatients.Count} imported patient(s)...");
            foreach (var imported in importedPatients)
            {
                var (patientId, bundleCount) = await ProcessImportedPatientAsync(
                    output,
                    fhirDataLoader,
                    manifestBuilder,
                    imported,
                    measures,
                    sharedSimEntries,
                    acquisitionSimulation,
                    generationClinicalPeriodStart,
                    generationClinicalPeriodEnd,
                    measureBundleJsons);

                importedPatientIds.Add(patientId);
                totalBundlesUploaded += bundleCount;
            }
        }

        // Build the final manifest
        var manifest = manifestBuilder.Build(measures);

        var generatedCount = profiles.Count;
        var importedCount = importedPatientIds.Count;
        output.WriteLine($"[Pipeline] Complete: {generatedCount} generated patient(s), {importedCount} imported patient(s), {totalBundlesUploaded} bundles uploaded.");

        var allPatientIds = new List<string>(generatedCount + importedCount);
        allPatientIds.AddRange(patientIds);
        allPatientIds.AddRange(importedPatientIds);

        return new PipelineResult
        {
            PatientIds = allPatientIds,
            Manifest = manifest,
            TotalBundlesUploaded = totalBundlesUploaded,
            GeneratedTemplateKeys = generatedTemplateKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList()
        };
    }

    /// <summary>
    /// Generates one additional qualifying patient, uploads their resources, and
    /// appends a new manifest row. Existing manifest rows are never rewritten.
    /// Shared infrastructure is reused from <paramref name="targetManifest"/> when a
    /// prior generated run-tag can be inferred; otherwise it is generated and uploaded.
    /// </summary>
    public static async Task<(string PatientId, PatientProfile Profile)> GenerateAndAppendPatientAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        GenerationManifest targetManifest,
        PatientProfile profile,
        IReadOnlyList<ProfiledMeasureType> measures,
        int totalResourcesPerPatient = FhirBundleGenerator.DefaultResourcesPerPatient,
        int? generationSeed = null,
        FhirGenerationConfig? config = null,
        GenerationRequirementsPlan? generationRequirementsPlan = null,
        AcquisitionSimulationConfig? acquisitionSimulation = null,
        IPatientEntryGenerator? patientEntryGenerator = null,
        ISharedInfrastructureGenerator? sharedInfrastructureGenerator = null,
        IGeneratedPatientTemplateCache? generatedTemplateCache = null,
        IReadOnlyList<string>? measureBundleJsons = null)
    {
        ArgumentNullException.ThrowIfNull(targetManifest);
        ArgumentNullException.ThrowIfNull(profile);
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));

        var (periodStart, periodEnd) = ParseClinicalPeriod(acquisitionSimulation);
        var inferredRunTag = TryInferRunTag(targetManifest.PatientIds);
        var uploadSharedInfrastructure = inferredRunTag == null;
        var runTag = inferredRunTag ?? Guid.NewGuid().ToString("N")[..8];
        var (sharedEntries, sharedPractitionerIds, sharedMedicationIds, ids) =
            GenerateSharedInfrastructure(generationRequirementsPlan, runTag, sharedInfrastructureGenerator);

        if (uploadSharedInfrastructure)
        {
            var sharedBundles = ChunkEntries(sharedEntries, "shared", 0);
            output.WriteLine($"[Pipeline] Uploading {sharedBundles.Count} shared infrastructure bundle(s) for mid-window generate...");
            await fhirDataLoader.UploadBundlesSequentiallyAsync(output, sharedBundles, "[shared] ");
        }

        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries = null;
        if (acquisitionSimulation != null)
            sharedSimEntries = BuildResourceIndex(sharedEntries);

        var patientIndex = NextGeneratedPatientIndex(targetManifest.PatientIds, runTag);
        var sliceBuilder = new GenerationManifest.IncrementalBuilder();
        if (uploadSharedInfrastructure)
            sliceBuilder.AddEntries(string.Empty, sharedEntries);

        var (patientId, _, _, _) = await GenerateAndUploadSinglePatientAsync(
            output,
            fhirDataLoader,
            sliceBuilder,
            profile,
            patientIndex,
            generationSeed.GetValueOrDefault(),
            totalResourcesPerPatient,
            measures,
            sharedPractitionerIds,
            sharedMedicationIds,
            sharedSimEntries,
            acquisitionSimulation,
            periodStart,
            periodEnd,
            config,
            generationRequirementsPlan,
            ids,
            generatedTemplateCache,
            patientEntryGenerator,
            measureBundleJsons);

        var slice = sliceBuilder.Build(measures);
        targetManifest.AppendFrom(slice);

        var effectiveProfile = ResolveProfile(targetManifest, patientId) ?? profile;
        output.WriteLine($"[Pipeline] Mid-window generate appended Patient/{patientId} to GenerationManifest.");
        return (patientId, effectiveProfile);
    }

    /// <summary>
    /// Imports one additional patient (upload bundle or existing FHIR ID) through the
    /// same classification / simulator path as start-of-run imports and appends a
    /// manifest row. Existing rows are never rewritten.
    /// </summary>
    public static async Task<(string PatientId, PatientProfile Profile)> ImportAndAppendPatientAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        GenerationManifest targetManifest,
        ImportedPatientInput imported,
        IReadOnlyList<ProfiledMeasureType> measures,
        AcquisitionSimulationConfig? acquisitionSimulation = null,
        IReadOnlyList<string>? measureBundleJsons = null)
    {
        ArgumentNullException.ThrowIfNull(targetManifest);
        ArgumentNullException.ThrowIfNull(imported);
        if (measures == null || measures.Count == 0)
            throw new ArgumentException("At least one measure is required.", nameof(measures));

        var (periodStart, periodEnd) = ParseClinicalPeriod(acquisitionSimulation);
        var sliceBuilder = new GenerationManifest.IncrementalBuilder();
        var (patientId, _) = await ProcessImportedPatientAsync(
            output,
            fhirDataLoader,
            sliceBuilder,
            imported,
            measures,
            sharedSimEntries: null,
            acquisitionSimulation,
            periodStart,
            periodEnd,
            measureBundleJsons);

        var slice = sliceBuilder.Build(measures);
        targetManifest.AppendFrom(slice);

        var effectiveProfile = ResolveProfile(targetManifest, patientId)
            ?? new PatientProfile(imported.MeasureEligibilities);
        output.WriteLine($"[Pipeline] Mid-window import appended Patient/{patientId} to GenerationManifest.");
        return (patientId, effectiveProfile);
    }

    public static string? TryInferRunTag(IEnumerable<string> patientIds)
    {
        foreach (var id in patientIds ?? [])
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("Patient-", StringComparison.Ordinal))
                continue;
            var rest = id["Patient-".Length..];
            var dash = rest.IndexOf('-');
            if (dash != 8)
                continue;
            var tag = rest[..8];
            if (IsSafeRunTagForTemplateCache(tag))
                return tag;
        }

        return null;
    }

    public static int NextGeneratedPatientIndex(IEnumerable<string> patientIds, string runTag)
    {
        var prefix = $"Patient-{runTag}-";
        var max = 0;
        foreach (var id in patientIds ?? [])
        {
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            if (int.TryParse(id[prefix.Length..], out var n) && n > max)
                max = n;
        }

        return max;
    }

    /// <summary>
    /// Replays a cached generation template into a collection Bundle, substituting
    /// this run's resource-ID tag for the placeholder stored in ABS.
    /// </summary>
    public static string MaterializeTemplateCollection(GeneratedPatientTemplate template, string runTag)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(runTag))
            throw new ArgumentException("Run tag is required to materialize a generated bundle.", nameof(runTag));

        var materialized = template.BundleJson
            .Select(json => ReplaceRunTag(json, template.TemplateRunTag, runTag))
            .ToList();
        return GeneratedPatientBundleJson.MergeToCollection(materialized);
    }

    private static (DateTime? Start, DateTime? End) ParseClinicalPeriod(AcquisitionSimulationConfig? acquisitionSimulation)
    {
        DateTime? start = null;
        DateTime? end = null;
        if (acquisitionSimulation == null)
            return (start, end);

        if (!string.IsNullOrWhiteSpace(acquisitionSimulation.ClinicalPeriodStart)
            && DateTimeOffset.TryParse(acquisitionSimulation.ClinicalPeriodStart,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rs))
        {
            start = rs.UtcDateTime;
        }

        if (!string.IsNullOrWhiteSpace(acquisitionSimulation.ClinicalPeriodEnd)
            && DateTimeOffset.TryParse(acquisitionSimulation.ClinicalPeriodEnd,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var re))
        {
            end = re.UtcDateTime;
        }

        return (start, end);
    }

    private static PatientProfile? ResolveProfile(GenerationManifest manifest, string patientId)
    {
        for (var i = 0; i < manifest.PatientIds.Count && i < manifest.Profiles.Count; i++)
        {
            if (string.Equals(manifest.PatientIds[i], patientId, StringComparison.Ordinal))
                return manifest.Profiles[i];
        }

        return null;
    }

    /// <summary>
    /// Generates a single patient's FHIR resources, uploads them, accumulates manifest
    /// metadata, optionally runs acquisition simulation, then discards all FHIR data.
    /// </summary>
    private static async Task<(string PatientId, PatientProfile Profile, int BundleCount, string TemplateKey)> GenerateAndUploadSinglePatientAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        GenerationManifest.IncrementalBuilder manifestBuilder,
        PatientProfile profile,
        int patientIndex,
        int baseSeed,
        int totalResourcesPerPatient,
        IReadOnlyList<ProfiledMeasureType> measures,
        List<string> sharedPractitionerIds,
        List<string> sharedMedicationIds,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries,
        AcquisitionSimulationConfig? acquisitionSimulation,
        DateTime? generationClinicalPeriodStart,
        DateTime? generationClinicalPeriodEnd,
        FhirGenerationConfig? config,
        GenerationRequirementsPlan? generationRequirementsPlan,
        FhirBundleGenerator.SharedIds ids,
        IGeneratedPatientTemplateCache? generatedTemplateCache,
        IPatientEntryGenerator? patientEntryGenerator,
        IReadOnlyList<string>? measureBundleJsons = null)
    {
        var patientSeed = baseSeed + (profile.SeedOffset ?? patientIndex);
        var patientId = ids.PatientId(patientIndex);

        // Respect per-profile resources override when present (e.g., cohort min/max expansion).
        // Falls back to run-level default for profiles that do not specify a concrete target.
        var effectiveResourcesPerPatient = profile.ResourcesPerPatient ?? totalResourcesPerPatient;

        var templateCacheKey = ComputeTemplateCacheKey(
            profile,
            patientIndex,
            baseSeed,
            effectiveResourcesPerPatient,
            measures,
            generationClinicalPeriodStart,
            generationClinicalPeriodEnd,
            config,
            generationRequirementsPlan);
        manifestBuilder.SetTemplateCacheKey(patientId, templateCacheKey);

        List<Bundle.EntryComponent> entries;
        List<(string Name, string Json)> bundles;

        var cachedTemplate = generatedTemplateCache == null
            ? null
            : await generatedTemplateCache.GetAsync(templateCacheKey);

        if (cachedTemplate == null)
        {
            var generator = patientEntryGenerator ?? ThetisPatientEntryGenerator.Shared;
            entries = await generator.GenerateAsync(new PatientEntryRequest
            {
                Profile = profile,
                PatientIndex = patientIndex,
                BaseSeed = baseSeed,
                TotalResourcesPerPatient = effectiveResourcesPerPatient,
                SharedPractitionerIds = sharedPractitionerIds,
                SharedMedicationIds = sharedMedicationIds,
                Measures = measures,
                ClinicalPeriodStart = generationClinicalPeriodStart,
                ClinicalPeriodEnd = generationClinicalPeriodEnd,
                Config = config,
                RequirementsPlan = generationRequirementsPlan,
                Ids = ids,
                Output = output
            });

            bundles = ChunkEntries(entries, patientId, 0);

            if (generatedTemplateCache != null)
            {
                var templateBundles = bundles.Select(b => ReplaceRunTag(b.Json, ids.RunTag, TemplateRunTag)).ToList();
                await generatedTemplateCache.StoreAsync(templateCacheKey, new GeneratedPatientTemplate(TemplateRunTag, templateBundles));
                if (ShouldEmitDetailedPatientLog(patientIndex))
                    output.WriteLine($"  [cache] Miss for {patientId}; stored template key={templateCacheKey}.");
            }
        }
        else
        {
            var materialized = cachedTemplate.BundleJson
                .Select(json => ReplaceRunTag(json, cachedTemplate.TemplateRunTag, ids.RunTag))
                .ToList();

            bundles = materialized
                .Select((json, idx) => (Name: $"{patientId}-bundle-{idx + 1:D3}", Json: json))
                .ToList();

            entries = ParseBundleEntriesFromJson(materialized);
            if (ShouldEmitDetailedPatientLog(patientIndex))
                output.WriteLine($"  [cache] Hit for {patientId}; reused template key={templateCacheKey}.");
        }

        var scenario = FhirGenerationCodes.GetScenarioById(profile.ClinicalScenarioId)
                       ?? FhirGenerationCodes.GetScenarioBySeed(patientSeed);

        DateTime encStart, encEnd;
        (encStart, encEnd) = DeriveEncounterWindowForProfile(
            profile,
            patientSeed,
            generationClinicalPeriodStart,
            generationClinicalPeriodEnd);

        var encounterId = $"{patientId}-Enc-001";
        // Compute per-resource CQL SDE filter exclusions from the actual generated resources
        // (inspects in-memory Encounter + Condition attributes — no seed replay).
        //
        // Filter rules apply only for measures this patient qualifies for. A non-qualifying
        // measure's MeasureReport does not contain the patient's resources, so its SDE
        // semantics do not contribute to the intersection of exclusions that determines
        // whether a resource reaches ABS.
        var effectiveProfile = IndexPatientEntries(
            manifestBuilder,
            patientId,
            profile,
            entries,
            measures,
            sharedSimEntries,
            acquisitionSimulation,
            generationClinicalPeriodStart,
            generationClinicalPeriodEnd,
            output,
            measureBundleJsons);

        if (ShouldEmitDetailedPatientLog(patientIndex))
        {
            output.WriteLine($"  Patient {patientId}: {entries.Count} entries [{FormatMeasureEligibilityLabel(measures, effectiveProfile)}] | scenario={scenario.PrimaryDxDisplay} | " +
                             $"encounter={encounterId} ({encStart:yyyy-MM-dd} ? {encEnd:yyyy-MM-dd})");
        }

        // Entries list is no longer needed — allow GC before upload
        entries.Clear();

        var progress = $"[{patientId}] ";
        await fhirDataLoader.UploadBundlesSequentiallyAsync(output, bundles, progress, logSuccessfulPosts: false);

        var bundleCount = bundles.Count;

        // Bundles are no longer needed — allow GC
        bundles.Clear();

        return (patientId, profile, bundleCount, templateCacheKey);
    }

    /// <summary>
    /// Processes a single imported patient: materializes their FHIR entries (either by fetching
    /// from the FHIR server using <c>Patient/{id}/$everything</c> or by parsing a supplied
    /// transaction bundle), runs the same manifest / CQL-filter / acquisition-simulator passes
    /// as generated patients, and (for bundle imports) uploads the data to the FHIR server.
    ///
    /// Failures throw and abort the run, matching the user-stated requirement that any imported
    /// patient that cannot be located or parsed must fail the scenario.
    /// </summary>
    private static async Task<(string PatientId, int BundleCount)> ProcessImportedPatientAsync(
        IAutomationOutput output,
        FhirDataLoader fhirDataLoader,
        GenerationManifest.IncrementalBuilder manifestBuilder,
        ImportedPatientInput imported,
        IReadOnlyList<ProfiledMeasureType> measures,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries,
        AcquisitionSimulationConfig? acquisitionSimulation,
        DateTime? generationClinicalPeriodStart,
        DateTime? generationClinicalPeriodEnd,
        IReadOnlyList<string>? measureBundleJsons = null)
    {
        if (imported == null)
            throw new ArgumentNullException(nameof(imported));
        if (string.IsNullOrWhiteSpace(imported.PatientId))
            throw new InvalidOperationException("Imported patient is missing PatientId.");

        var patientId = imported.PatientId.Trim();

        // 1. Materialize entries — reuse the pre-loaded list when the runner has
        //    already fetched / parsed them (so the clinical period could be widened
        //    based on actual encounter dates). Otherwise fetch / parse on demand.
        List<Bundle.EntryComponent> entries;
        if (imported.PreLoadedEntries is { Count: > 0 })
        {
            entries = imported.PreLoadedEntries;
        }
        else
        {
            string bundleJson;
            if (imported.Source == ImportedPatientSource.ExistingId)
            {
                output.WriteLine($"  [imported:id] Fetching Patient/{patientId}/$everything from FHIR server...");
                bundleJson = await fhirDataLoader.FetchPatientEverythingAsync(patientId);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(imported.BundleJson))
                    throw new InvalidOperationException($"Imported bundle for patient '{patientId}' has no bundle JSON.");
                bundleJson = imported.BundleJson!;
                output.WriteLine($"  [imported:bundle] Parsing supplied bundle for Patient/{patientId} ({imported.FileName ?? "(no file)"})...");
            }

            entries = ImportedPatientLoader.ParseBundleEntries(bundleJson, patientId);
        }

        if (entries.Count == 0)
            throw new InvalidOperationException($"Imported patient '{patientId}' produced no FHIR entries.");

        // 2. Build per-measure eligibility (auto-detect when requested; user override wins).
        var eligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>();
        foreach (var m in measures)
            eligibilities[m] = MeasureEligibility.NonQualifying;

        if (imported.AutoDetect)
        {
            var detection = ImportedPatientClassifier.Classify(entries, measures);
            foreach (var (m, e) in detection.MeasureEligibilities)
                eligibilities[m] = e;
        }

        // User overrides (always take precedence over auto-detection).
        if (imported.MeasureEligibilities != null)
        {
            foreach (var (m, e) in imported.MeasureEligibilities)
                eligibilities[m] = e;
        }

        var profile = new PatientProfile(eligibilities, ClinicalScenarioId: imported.DetectedClinicalScenarioId);

        var effectiveProfile = IndexPatientEntries(
            manifestBuilder,
            patientId,
            profile,
            entries,
            measures,
            sharedSimEntries,
            acquisitionSimulation,
            generationClinicalPeriodStart,
            generationClinicalPeriodEnd,
            output,
            measureBundleJsons);

        output.WriteLine($"  [imported] Patient {patientId}: {entries.Count} entries [{FormatMeasureEligibilityLabel(measures, effectiveProfile)}] | source={imported.Source}");

        // 6. Upload (bundle imports) or mark as pre-existing (id imports)
        var bundleCount = 0;
        if (imported.Source == ImportedPatientSource.Bundle)
        {
            var bundles = ChunkEntries(entries, patientId, 0);
            entries.Clear();
            var ok = await fhirDataLoader.UploadBundlesSequentiallyAsync(output, bundles, $"[imported:{patientId}] ", logSuccessfulPosts: false);
            if (!ok)
                throw new InvalidOperationException($"Failed to upload imported bundle for patient '{patientId}'.");
            bundleCount = bundles.Count;
            bundles.Clear();
        }
        else
        {
            manifestBuilder.MarkPreExistingPatient(patientId);
            entries.Clear();
        }

        return (patientId, bundleCount);
    }

    /// <summary>
    /// Shared generated/imported post-processing: period eligibility, CQL SDE keys,
    /// acquisition simulation, and manifest rows. Callers still own materialization
    /// (Thetis vs fetch/parse) and whether to POST the bundles.
    /// </summary>
    private static PatientProfile IndexPatientEntries(
        GenerationManifest.IncrementalBuilder manifestBuilder,
        string patientId,
        PatientProfile profile,
        List<Bundle.EntryComponent> entries,
        IReadOnlyList<ProfiledMeasureType> measures,
        List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries,
        AcquisitionSimulationConfig? acquisitionSimulation,
        DateTime? generationClinicalPeriodStart,
        DateTime? generationClinicalPeriodEnd,
        IAutomationOutput output,
        IReadOnlyList<string>? measureBundleJsons = null)
    {
        HashSet<string>? cqlFilteredKeys = null;
        var cqlInput = CqlFilterInputExtractor.ExtractFromEntries(patientId, entries);
        var effectiveProfile = profile;

        if (cqlInput != null)
        {
            if (generationClinicalPeriodStart.HasValue || generationClinicalPeriodEnd.HasValue)
            {
                cqlInput = cqlInput with
                {
                    MeasurementPeriodStart = generationClinicalPeriodStart ?? DateTime.MinValue,
                    MeasurementPeriodEnd = generationClinicalPeriodEnd ?? DateTime.MaxValue
                };
            }

            effectiveProfile = ApplyMeasurementPeriodEligibilityPrediction(
                patientId,
                profile,
                measures,
                cqlInput,
                generationClinicalPeriodStart,
                generationClinicalPeriodEnd,
                output);

            var qualifyingMeasures = measures.Where(effectiveProfile.QualifiesFor).ToList();
            if (qualifyingMeasures.Count > 0)
            {
                var bundles = ResolveMeasureBundles(measureBundleJsons, measures, qualifyingMeasures);
                cqlFilteredKeys = CqlFilterSimulator.ComputeFilteredKeys(bundles, cqlInput);
                manifestBuilder.SetCqlFilteredKeys(patientId, cqlFilteredKeys);
            }
        }

        manifestBuilder.AddPatient(patientId, effectiveProfile);
        manifestBuilder.AddEntries(patientId, entries);

        if (acquisitionSimulation != null)
        {
            var patientSimEntries = BuildResourceIndex(entries);
            var acquiredKeys = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysForPatient(
                patientId,
                patientSimEntries,
                sharedSimEntries,
                acquisitionSimulation.QueryPlan,
                acquisitionSimulation.ClinicalPeriodStart,
                acquisitionSimulation.ClinicalPeriodEnd,
                output,
                acquisitionSimulation.AllowEncounterAnchoredDateOverrideForOutOfRange);
            acquiredKeys = OrgResourceMapPredictionFilter.Apply(
                acquiredKeys,
                patientSimEntries,
                sharedSimEntries,
                acquisitionSimulation.OrganizationLocationConditionFhirPaths,
                cqlFilteredKeys);
            manifestBuilder.SetSimulatedAcquiredKeys(patientId, acquiredKeys);
        }

        return effectiveProfile;
    }

    private static string FormatMeasureEligibilityLabel(
        IReadOnlyList<ProfiledMeasureType> measures,
        PatientProfile profile)
        => string.Join(", ", measures.Select(m =>
        {
            var shortName = m switch
            {
                ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "ACH",
                ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "ACH-Daily",
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Hypo",
                _ => m.ToString()
            };
            return $"{shortName}={(profile.QualifiesFor(m) ? "Q" : "NQ")}";
        }));

    private static PatientProfile ApplyMeasurementPeriodEligibilityPrediction(
        string patientId,
        PatientProfile profile,
        IReadOnlyList<ProfiledMeasureType> measures,
        CqlFilterSimulator.PatientCqlInput cqlInput,
        DateTime? measurementPeriodStart,
        DateTime? measurementPeriodEnd,
        IAutomationOutput output)
    {
        if (!measurementPeriodStart.HasValue || !measurementPeriodEnd.HasValue)
            return profile;

        var constrainedInput = cqlInput with
        {
            MeasurementPeriodStart = measurementPeriodStart.Value,
            MeasurementPeriodEnd = measurementPeriodEnd.Value
        };

        var adjusted = new Dictionary<ProfiledMeasureType, MeasureEligibility>(profile.MeasureEligibilities);
        var downgraded = new List<string>();

        foreach (var measure in measures)
        {
            if (!adjusted.TryGetValue(measure, out var eligibility)
                || eligibility != MeasureEligibility.Qualifying)
            {
                continue;
            }

            var hasInPeriodIpOverlap = MeasureInitialPopulationResolver.Resolve([measure], constrainedInput).Count > 0;
            if (hasInPeriodIpOverlap)
                continue;

            adjusted[measure] = MeasureEligibility.NonQualifying;
            downgraded.Add(measure.ToString());
        }

        if (downgraded.Count > 0)
        {
            output.WriteLine(
                $"  [prediction] Patient {patientId}: downgraded to NQ for {string.Join(", ", downgraded)} due to no initial-population encounter overlap with report period.");
        }

        return profile with { MeasureEligibilities = adjusted };
    }

    private static IReadOnlyList<string> ResolveMeasureBundles(
        IReadOnlyList<string>? measureBundleJsons,
        IReadOnlyList<ProfiledMeasureType> allMeasures,
        IReadOnlyList<ProfiledMeasureType> qualifyingMeasures)
    {
        if (measureBundleJsons != null
            && allMeasures.Count > 0
            && measureBundleJsons.Count == allMeasures.Count)
        {
            return allMeasures
                .Select((measure, index) => (measure, json: measureBundleJsons[index]))
                .Where(pair => qualifyingMeasures.Contains(pair.measure) && !string.IsNullOrWhiteSpace(pair.json))
                .Select(pair => pair.json)
                .ToList();
        }

        if (measureBundleJsons != null
            && measureBundleJsons.Count > 0
            && measureBundleJsons.Count == qualifyingMeasures.Count)
        {
            return measureBundleJsons.Where(json => !string.IsNullOrWhiteSpace(json)).ToList();
        }

        return qualifyingMeasures.Select(measure => ProfiledMeasureCatalog.ReadBundleJson(measure)).ToList();
    }

    // ------------------------------------------------------------------
    //  Shared infrastructure generation
    // ------------------------------------------------------------------

    private static (List<Bundle.EntryComponent> Entries, List<string> PractitionerIds, List<string> MedicationIds, FhirBundleGenerator.SharedIds Ids)
        GenerateSharedInfrastructure(
            GenerationRequirementsPlan? generationRequirementsPlan,
            string runTag,
            ISharedInfrastructureGenerator? sharedInfrastructureGenerator = null)
    {
        // All shared-infrastructure construction lives in ScenarioResourceGeneration so
        // FhirBundleGenerator (bulk path) and FhirGenerationPipeline (streaming path)
        // can never drift on shared-resource shape, IDs, or order. Thetis (KD21) uses
        // the same factory generator until a shared-infra graph exists.
        var generator = sharedInfrastructureGenerator ?? FactorySharedInfrastructureGenerator.Shared;
        var (ids, entries, practitionerIds, medicationIds) =
            generator.Generate(generationRequirementsPlan, runTag);
        return (entries, practitionerIds, medicationIds, ids);
    }

    private static string ComputeTemplateCacheKey(
        PatientProfile profile,
        int patientIndex,
        int baseSeed,
        int resourcesPerPatient,
        IReadOnlyList<ProfiledMeasureType> measures,
        DateTime? periodStart,
        DateTime? periodEnd,
        FhirGenerationConfig? config,
        GenerationRequirementsPlan? requirements)
    {
        var keyPayload = JsonSerializer.Serialize(new
        {
            PatientProfile = profile,
            PatientIndex = patientIndex,
            BaseSeed = baseSeed,
            ResourcesPerPatient = resourcesPerPatient,
            Measures = measures.Select(m => m.ToString()).ToArray(),
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Config = config,
            Requirements = requirements,
            Generator = "thetis",
            GeneratorDependencyFingerprint = GeneratorDependencyFingerprint.Value
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(keyPayload));
        return Convert.ToHexString(hash);
    }

    private static string ComputeGeneratorDependencyFingerprint()
    {
        var assembly = typeof(FhirGenerationPipeline).Assembly;
        var dependencyNames = new[]
        {
            "Automation",
            "Hl7.Fhir.Base",
            "Hl7.Fhir.Support",
            "Thetis.Generation.Engine",
            "Thetis.Generation.Abstractions"
        };

        var dependencies = assembly
            .GetReferencedAssemblies()
            .Where(reference => dependencyNames.Contains(reference.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(reference => reference.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var payload = new StringBuilder();
        payload.Append("pipeline=").Append(GetAssemblyIdentityHash(assembly));

        foreach (var dependency in dependencies)
        {
            payload.Append('|')
                .Append(dependency.Name)
                .Append('=')
                .Append(dependency.Version?.ToString() ?? "none")
                .Append(':')
                .Append(GetLoadedAssemblyHash(dependency.Name ?? string.Empty));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString()));
        return Convert.ToHexString(hash);
    }

    private static string GetLoadedAssemblyHash(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return "none";

        var loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));

        return loaded == null ? "unloaded" : GetAssemblyIdentityHash(loaded);
    }

    private static string GetAssemblyIdentityHash(Assembly assembly)
    {
        var version = assembly.GetName().Version?.ToString() ?? "none";
        var moduleVersion = assembly.ManifestModule.ModuleVersionId.ToString("N");
        var locationHash = string.Empty;

        if (!string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location))
        {
            try
            {
                using var stream = File.OpenRead(assembly.Location);
                var bytes = SHA256.HashData(stream);
                locationHash = Convert.ToHexString(bytes);
            }
            catch
            {
                locationHash = "io-error";
            }
        }

        var payload = $"{assembly.GetName().Name}:{version}:{moduleVersion}:{locationHash}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string ReplaceRunTag(string json, string sourceRunTag, string targetRunTag)
    {
        if (!IsAllowedTemplateReplacementTag(sourceRunTag) || !IsAllowedTemplateReplacementTag(targetRunTag))
            return json;

        if (string.Equals(sourceRunTag, targetRunTag, StringComparison.Ordinal))
            return json;

        return json.Replace(sourceRunTag, targetRunTag, StringComparison.Ordinal);
    }

    private static bool IsAllowedTemplateReplacementTag(string runTag)
    {
        return string.Equals(runTag, TemplateRunTag, StringComparison.Ordinal)
            || IsSafeRunTagForTemplateCache(runTag);
    }

    private static bool IsSafeRunTagForTemplateCache(string? runTag)
    {
        if (string.IsNullOrWhiteSpace(runTag) || runTag.Length != 8)
            return false;

        foreach (var ch in runTag)
        {
            if (!Uri.IsHexDigit(ch))
                return false;
        }

        return true;
    }

    private static List<Bundle.EntryComponent> ParseBundleEntriesFromJson(IReadOnlyList<string> bundleJson)
    {
        var parser = new FhirJsonDeserializer(new DeserializerSettings().UsingMode(DeserializationMode.Ostrich));
        var entries = new List<Bundle.EntryComponent>();

        foreach (var json in bundleJson)
        {
            var bundle = parser.Deserialize<Bundle>(json);
            if (bundle?.Entry is { Count: > 0 })
            {
                entries.AddRange(bundle.Entry.Where(entry => entry?.Resource != null));
            }
        }

        return entries;
    }

    internal static (DateTime Start, DateTime End) DeriveEncounterWindowForProfile(
        PatientProfile profile,
        int seed,
        DateTime? clinicalPeriodStart,
        DateTime? clinicalPeriodEnd)
    {
        if (!profile.RequiresInpatientEncounter())
        {
            return FhirBundleGenerator.DeriveOutpatientEncounterWindow(seed, clinicalPeriodStart, clinicalPeriodEnd);
        }

        if (profile.ScheduledInpatientPattern.HasValue
            && clinicalPeriodStart.HasValue
            && clinicalPeriodEnd.HasValue
            && clinicalPeriodEnd.Value > clinicalPeriodStart.Value)
        {
            return DeriveScheduledPatternInpatientWindow(
                profile.ScheduledInpatientPattern.Value,
                seed,
                clinicalPeriodStart.Value,
                clinicalPeriodEnd.Value);
        }

        return FhirBundleGenerator.DeriveInpatientEncounterWindow(seed, clinicalPeriodStart, clinicalPeriodEnd);
    }

    private static (DateTime Start, DateTime End) DeriveScheduledPatternInpatientWindow(
        ScheduledInpatientPattern pattern,
        int seed,
        DateTime reportStart,
        DateTime reportEnd)
    {
        var rs = DateTime.SpecifyKind(reportStart, DateTimeKind.Utc);
        var re = DateTime.SpecifyKind(reportEnd, DateTimeKind.Utc);

        if (re <= rs)
            return FhirBundleGenerator.DeriveInpatientEncounterWindow(seed, rs, re);

        var period = re - rs;
        var totalMinutes = Math.Max(1, (int)period.TotalMinutes);

        // Keep deterministic placement but avoid minute-scale stays that are too sparse to
        // reliably satisfy downstream measure criteria in scheduled scenarios.
        var admissionOffsetMinutes = Math.Max(5, (int)Math.Round(totalMinutes * 0.20));
        var dischargeOffsetMinutes = Math.Max(admissionOffsetMinutes + 30, (int)Math.Round(totalMinutes * 0.75));

        // Seed-driven jitter to prevent all scheduled patients from sharing identical timestamps.
        var jitter = Math.Abs(seed % 20);

        var inPeriodStart = rs.AddMinutes(Math.Min(totalMinutes - 1, admissionOffsetMinutes + jitter));
        var inPeriodEnd = rs.AddMinutes(Math.Min(totalMinutes - 1, dischargeOffsetMinutes + jitter));
        if (inPeriodEnd <= inPeriodStart)
            inPeriodEnd = inPeriodStart.AddMinutes(30);

        // Padding used for "before" / "after" patterns. Ensure at least 6h separation from
        // report boundaries when the report window is reasonably sized.
        var boundaryPad = period.TotalHours >= 12
            ? TimeSpan.FromHours(6)
            : TimeSpan.FromMinutes(Math.Max(60, totalMinutes / 6));

        return pattern switch
        {
            ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod
                => (rs - boundaryPad, re + boundaryPad),

            ScheduledInpatientPattern.AdmittedBeforePeriodDischargedDuringPeriod
                => (rs - boundaryPad, inPeriodEnd),

            ScheduledInpatientPattern.AdmittedDuringPeriodRemainsInpatientAfterPeriod
                => (inPeriodStart, re + boundaryPad),

            ScheduledInpatientPattern.AdmittedDuringPeriodDischargedDuringPeriod
                => (inPeriodStart, inPeriodEnd),

            ScheduledInpatientPattern.AdmittedAndDischargedBeforePeriod
                => (rs - (boundaryPad + TimeSpan.FromHours(6)), rs - TimeSpan.FromHours(1)),

            ScheduledInpatientPattern.AdmittedAndDischargedAfterPeriod
                => (re + TimeSpan.FromHours(1), re + (boundaryPad + TimeSpan.FromHours(6))),

            _ => FhirBundleGenerator.DeriveInpatientEncounterWindow(seed, rs, re)
        };
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

    private static bool ShouldEmitDetailedPatientLog(int patientIndex)
    {
        if (patientIndex < VerbosePatientLogHeadCount)
            return true;

        var ordinal = patientIndex + 1;
        return ordinal % VerbosePatientLogInterval == 0;
    }
}
