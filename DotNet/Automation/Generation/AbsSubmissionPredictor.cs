using Hl7.Fhir.Model;
using LantanaGroup.Automation.Helpers;
using System.Text.Json;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Runs the same generation-time ABS prediction the pipeline uses (CQL instance
/// filter + query-plan acquisition simulation + type gates) without uploading
/// to FHIR. Tests replay imported bundles / Run Diagnostics artifacts through
/// this helper and compare <see cref="GenerationManifest.GetExpectedAbsCountsForPatient"/>
/// to the ABS type counts captured for that run.
/// </summary>
public static class AbsSubmissionPredictor
{
    /// <summary>
    /// Populates <paramref name="manifestBuilder"/> for one patient: period-aware
    /// Q/NQ, CQL SDE exclusions, generated keys, and simulated-acquired keys.
    /// Returns the eligibility profile after measurement-period adjustment.
    /// </summary>
    public static PatientProfile PopulateManifest(
        GenerationManifest.IncrementalBuilder manifestBuilder,
        string patientId,
        PatientProfile profile,
        IReadOnlyList<Bundle.EntryComponent> entries,
        IReadOnlyList<ProfiledMeasureType> measures,
        FhirGenerationPipeline.AcquisitionSimulationConfig? acquisitionSimulation,
        DateTime? measurementPeriodStart,
        DateTime? measurementPeriodEnd,
        IReadOnlyList<(string ResourceType, string ResourceId, string Key, JsonElement Resource)>? sharedSimEntries,
        IAutomationOutput? output)
    {
        HashSet<string>? cqlFilteredKeys = null;
        var cqlInput = CqlFilterInputExtractor.ExtractFromEntries(patientId, entries, sharedSimEntries);
        var effectiveProfile = profile;

        if (cqlInput != null)
        {
            if (measurementPeriodStart.HasValue || measurementPeriodEnd.HasValue)
            {
                cqlInput = cqlInput with
                {
                    MeasurementPeriodStart = measurementPeriodStart ?? DateTime.MinValue,
                    MeasurementPeriodEnd = measurementPeriodEnd ?? DateTime.MaxValue
                };
            }

            effectiveProfile = ApplyMeasurementPeriodEligibilityPrediction(
                patientId,
                profile,
                measures,
                cqlInput,
                measurementPeriodStart,
                measurementPeriodEnd,
                output);

            var qualifyingMeasures = measures.Where(effectiveProfile.QualifiesFor).ToList();
            if (qualifyingMeasures.Count > 0)
            {
                cqlFilteredKeys = CqlFilterSimulator.ComputeFilteredKeys(qualifyingMeasures, cqlInput);
                manifestBuilder.SetCqlFilteredKeys(patientId, cqlFilteredKeys);
            }
        }

        manifestBuilder.AddPatient(patientId, effectiveProfile);
        manifestBuilder.AddEntries(patientId, entries);

        if (acquisitionSimulation != null)
        {
            var patientSimEntries = IndexEntries(entries);
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

    /// <summary>
    /// Builds a complete <see cref="GenerationManifest"/> for an imported patient
    /// bundle (searchset or transaction) using the default query plan and the
    /// selected measures' embedded CQL. Used by diagnostics-replay unit tests.
    /// </summary>
    public static GenerationManifest PredictImportedBundle(
        string bundleJson,
        string patientId,
        DateTime periodStart,
        DateTime periodEnd,
        IReadOnlyList<ProfiledMeasureType>? measures = null,
        QueryPlanInput? queryPlan = null,
        IAutomationOutput? output = null)
    {
        measures ??= [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];
        queryPlan ??= QueryPlanDefaults.GetDefaultAsInput();

        var entries = ImportedPatientLoader.ParseBundleEntries(bundleJson, patientId);
        var eligibilities = measures.ToDictionary(m => m, _ => MeasureEligibility.Qualifying);
        var profile = new PatientProfile(eligibilities);

        var startIso = periodStart.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var endIso = periodEnd.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var acquisition = new FhirGenerationPipeline.AcquisitionSimulationConfig
        {
            QueryPlan = queryPlan,
            ClinicalPeriodStart = startIso,
            ClinicalPeriodEnd = endIso
        };

        var builder = new GenerationManifest.IncrementalBuilder();
        PopulateManifest(
            builder,
            patientId,
            profile,
            entries,
            measures,
            acquisition,
            periodStart.ToUniversalTime(),
            periodEnd.ToUniversalTime(),
            sharedSimEntries: null,
            output);

        var manifest = builder.Build(measures);
        manifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(queryPlan);
        manifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(measures);
        return manifest;
    }

    /// <summary>
    /// Converts in-memory FHIR bundle entries into (ResourceType, ResourceId, Key, JsonElement)
    /// tuples for <see cref="QueryPlanAcquisitionSimulator"/>.
    /// </summary>
    public static List<(string ResourceType, string ResourceId, string Key, JsonElement Resource)> IndexEntries(
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

            var json = JsonSerializer.Serialize(entry.Resource, entry.Resource.GetType(), serializerOptions);
            using var doc = JsonDocument.Parse(json);
            result.Add((resourceType, resourceId, url, doc.RootElement.Clone()));
        }

        return result;
    }

    internal static PatientProfile ApplyMeasurementPeriodEligibilityPrediction(
        string patientId,
        PatientProfile profile,
        IReadOnlyList<ProfiledMeasureType> measures,
        CqlFilterSimulator.PatientCqlInput cqlInput,
        DateTime? measurementPeriodStart,
        DateTime? measurementPeriodEnd,
        IAutomationOutput? output)
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
            output?.WriteLine(
                $"  [prediction] Patient {patientId}: downgraded to NQ for {string.Join(", ", downgraded)} due to no initial-population encounter overlap with report period.");
        }

        return profile with { MeasureEligibilities = adjusted };
    }
}
