using Automation.UI.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automation.UI.Services;

/// <summary>
/// Pure helpers that turn a <see cref="StartScenarioRequest"/> (which may carry only
/// a saved-scenario JSON blob plus a handful of overrides) into a fully-populated
/// <see cref="ResolvedRunOptions"/> the run pipeline can consume directly.
///
/// Lives outside <see cref="AutomationRunManager"/> so the resolution rules are
/// independently unit-testable. No I/O; no DI; safe to call from anywhere.
/// </summary>
public static class StartScenarioRequestResolver
{
    private const string AdhocReportTestNhsnOrganizationId = "10756";
    private const string MultiPatientTestNhsnOrganizationId = "10758";
    private const string MegaPatientTestNhsnOrganizationId = "10759";

    private static readonly JsonSerializerOptions CohortJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Resolves the run options for a request. For non-Custom scenarios the static
    /// scenario-kind defaults win outright; Custom merges request overrides on top
    /// of defaults and falls back to extracting from <c>RunConfigurationJson</c>
    /// when typed properties are absent.
    /// </summary>
    public static ResolvedRunOptions Resolve(StartScenarioRequest request)
    {
        var defaultMeasures = new List<ProfiledMeasureType> { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        var defaults = request.Scenario switch
        {
            AutomationScenarioKind.AdhocReportTest => new ResolvedRunOptions(1, 1000, 20260326, 3, 0, 30, false, true, defaultMeasures, [], [])
            {
                NhsnOrganizationId = AdhocReportTestNhsnOrganizationId
            },
            AutomationScenarioKind.MultiPatientTest => new ResolvedRunOptions(1000, 100, 20260328, 3, 0, 30, false, true, defaultMeasures, [], [])
            {
                NhsnOrganizationId = MultiPatientTestNhsnOrganizationId
            },
            AutomationScenarioKind.MegaPatientTest => new ResolvedRunOptions(FhirBundleGenerator.DefaultPatientCount, FhirBundleGenerator.DefaultResourcesPerPatient, 20260327, 3, 0, 30, false, true, defaultMeasures, [], [])
            {
                NhsnOrganizationId = MegaPatientTestNhsnOrganizationId
            },
            AutomationScenarioKind.Custom => new ResolvedRunOptions(10, 250, 20260329, 3, 30, 30, false, true, defaultMeasures, [], [])
            {
                NhsnOrganizationId = GenerateRandomNhsnOrganizationId()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scenario), request.Scenario, null)
        };

        if (request.Scenario != AutomationScenarioKind.Custom)
        {
            // Built-in kinds still exist for API/history and to mirror BackendE2ETests.
            // Expand the same All-Qualifying cohort the executor used to synthesize so
            // generation has a single profile+import path.
            var kindCohorts = new List<PatientCohortDefinition>
            {
                BuildDefaultCohort(
                    defaultMeasures,
                    defaults.PatientCount,
                    defaults.ResourcesPerPatient,
                    defaults.ResourcesPerPatient)
            };
            return defaults with
            {
                PatientCohorts = kindCohorts,
                PatientProfiles = PatientCohortDefinition.ExpandProfiles(kindCohorts, defaults.Seed)
            };
        }

        var effectiveMeasures = request.SelectedMeasures is { Count: > 0 }
            ? request.SelectedMeasures
            : ExtractSelectedMeasuresFromJson(request.RunConfigurationJson)
              ?? defaults.SelectedMeasures;

        var cohorts = request.PatientCohorts is { Count: > 0 }
            ? request.PatientCohorts
            : ExtractCohortsFromJson(request.RunConfigurationJson, effectiveMeasures)
              ?? [
                  BuildDefaultCohort(
                      effectiveMeasures,
                      request.PatientCount ?? defaults.PatientCount,
                      defaultResourcesMin: 250,
                      defaultResourcesMax: 250)
              ];

        ApplyCohortDefaults(cohorts, effectiveMeasures);

        // Cohorts are the single source of truth for patient profiles; expand them.
        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, request.Seed ?? defaults.Seed);

        var importedIds = request.ImportedPatientIds is { Count: > 0 }
            ? request.ImportedPatientIds
            : ExtractImportedFromJson(request.RunConfigurationJson, "importedPatientIds")
              ?? [];

        var importedBundles = request.ImportedPatientBundles is { Count: > 0 }
            ? request.ImportedPatientBundles
            : ExtractImportedFromJson(request.RunConfigurationJson, "importedPatientBundles")
              ?? [];

        var (reportStart, reportEnd) = ResolveReportPeriod(request);

        var hasImportedPatients = importedIds.Count > 0 || importedBundles.Count > 0;
        var nhsnOrganizationId = ResolveNhsnOrganizationId(request, defaults.NhsnOrganizationId);
        var organizationResourceMapTemplateId = request.OrganizationResourceMapTemplateId ?? ExtractGuidFromJson(request.RunConfigurationJson, "organizationResourceMapTemplateId");
        var isLiveSimulation = request.IsLiveSimulation
            || ExtractBoolFromJson(request.RunConfigurationJson, "isLiveSimulation") == true;
        if (isLiveSimulation && !ReportExecution.IsLiveAllowed(request.ReportMethod))
        {
            throw new InvalidOperationException(
                "Live simulation is only valid for Scheduled reports. Adhoc uses a set patient list; Regenerate is an all-at-once Adhoc copy of a previous schedule.");
        }

        var scheduledLike = ReportExecution.UsesCensusScheduleKickoff(request.ReportMethod);
        if (hasImportedPatients && !scheduledLike && (!reportStart.HasValue || !reportEnd.HasValue))
        {
            throw new InvalidOperationException(
                "Report period start and end are required when imported patients are included in an adhoc run.");
        }
        var reportingWindowMinutes = NormalizeReportingWindowMinutes(
            request.ReportingWindowMinutes
            ?? ExtractIntFromJson(request.RunConfigurationJson, "reportingWindowMinutes"));
        var reportMethod = request.ReportMethod;

        return defaults with
        {
            PatientCount = request.PatientCount ?? defaults.PatientCount,
            // Legacy run-level ResourcesPerPatient is obsolete for cohort-based runs.
            // Keep the field populated for back-compat telemetry/UI callers.
            ResourcesPerPatient = cohorts.FirstOrDefault()?.ResourcesPerPatientMax ?? defaults.ResourcesPerPatient,
            Seed = request.Seed ?? defaults.Seed,
            PollingIntervalSeconds = 3,
            // Keep an explicit hard timeout for custom runs so scheduled workflows
            // fail-fast when end-of-period orchestration does not advance.
            // Live windows need the reporting window plus time for EOP/finalization.
            MaxPollingDurationMinutes = isLiveSimulation
                ? Math.Max(defaults.MaxPollingDurationMinutes, reportingWindowMinutes + 30)
                : defaults.MaxPollingDurationMinutes,
            LokiScrapeWindowMinutes = 30,
            CleanupServiceData = request.CleanupServiceData ?? defaults.CleanupServiceData,
            CleanupFhirData = request.CleanupFhirData ?? defaults.CleanupFhirData,
            SelectedMeasures = effectiveMeasures,
            PatientProfiles = profiles,
            PatientCohorts = cohorts,
            ReportMethod = reportMethod,
            QueryPlanTemplateId = request.QueryPlanTemplateId,
            NormalizationSuiteId = request.NormalizationSuiteId,
            OrganizationResourceMapTemplateId = organizationResourceMapTemplateId,
            ImportedPatientIds = importedIds,
            ImportedPatientBundles = importedBundles,
            ReportPeriodStart = reportStart,
            ReportPeriodEnd = reportEnd,
            NhsnOrganizationId = nhsnOrganizationId,
            IsLiveSimulation = isLiveSimulation,
            ReportingWindowMinutes = reportingWindowMinutes
        };
    }

    internal static int NormalizeReportingWindowMinutes(int? minutes)
    {
        if (!minutes.HasValue || minutes.Value <= 0)
            return 10;

        return minutes.Value switch
        {
            <= 5 => 5,
            <= 10 => 10,
            _ => 15
        };
    }

    private static Guid? ExtractGuidFromJson(string? runConfigurationJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(runConfigurationJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(runConfigurationJson);
            if (doc.RootElement.TryGetProperty(propertyName, out var prop)
                && prop.ValueKind == JsonValueKind.String
                && Guid.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }
        }
        catch
        {
            // fall through
        }

        return null;
    }

    private static bool? ExtractBoolFromJson(string? runConfigurationJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(runConfigurationJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(runConfigurationJson);
            if (!doc.RootElement.TryGetProperty(propertyName, out var prop))
                return null;

            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(prop.GetString(), out var parsed) => parsed,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static int? ExtractIntFromJson(string? runConfigurationJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(runConfigurationJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(runConfigurationJson);
            if (!doc.RootElement.TryGetProperty(propertyName, out var prop))
                return null;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var number))
                return number;

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                return parsed;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveNhsnOrganizationId(StartScenarioRequest request, string defaultValue)
    {
        if (!string.IsNullOrWhiteSpace(request.NhsnOrganizationId))
            return request.NhsnOrganizationId.Trim();

        if (!string.IsNullOrWhiteSpace(request.RunConfigurationJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(request.RunConfigurationJson);
                if (doc.RootElement.TryGetProperty("nhsnOrganizationId", out var org)
                    && org.ValueKind == JsonValueKind.String)
                {
                    var value = org.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value.Trim();
                }
            }
            catch
            {
                // Fall through to default.
            }
        }

        return defaultValue;
    }

    private static string GenerateRandomNhsnOrganizationId()
    {
        return Random.Shared.Next(10000, 100000).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static PatientCohortDefinition BuildDefaultCohort(
        IReadOnlyList<ProfiledMeasureType> measures,
        int patientCount,
        int defaultResourcesMin,
        int defaultResourcesMax)
    {
        var cohort = PatientCohortDefinition.AllQualifying(measures, patientCount, defaultResourcesMin, defaultResourcesMax);
        cohort.ScheduledInpatientPattern = ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;
        return cohort;
    }

    private static void ApplyCohortDefaults(
        IReadOnlyList<PatientCohortDefinition> cohorts,
        IReadOnlyList<ProfiledMeasureType> selectedMeasures)
    {
        foreach (var cohort in cohorts)
        {
            cohort.ScheduledInpatientPattern ??= ScheduledInpatientPattern.AdmittedBeforePeriodRemainsInpatientAfterPeriod;

            var allNonQualifying = selectedMeasures.Count > 0
                && selectedMeasures.All(m => cohort.GetEligibility(m) == MeasureEligibility.NonQualifying);
            if (allNonQualifying)
                cohort.CohortQualification = MeasureEligibility.NonQualifying;
        }
    }

    /// <summary>
    /// Resolves the run's reporting period. The request's explicit values win; otherwise we
    /// pull from the saved scenario JSON. When neither is set, the period is null and
    /// <see cref="ScenarioConfigBuilder"/> falls back to its hard-coded default.
    /// </summary>
    private static (DateTimeOffset? Start, DateTimeOffset? End) ResolveReportPeriod(StartScenarioRequest request)
    {
        var start = request.ReportPeriodStart;
        var end = request.ReportPeriodEnd;

        if ((start.HasValue && end.HasValue) || string.IsNullOrWhiteSpace(request.RunConfigurationJson))
            return (start, end);

        try
        {
            using var doc = JsonDocument.Parse(request.RunConfigurationJson);
            if (!start.HasValue && doc.RootElement.TryGetProperty("reportPeriodStart", out var s) && s.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(s.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedStart))
            {
                start = parsedStart;
            }

            if (!end.HasValue && doc.RootElement.TryGetProperty("reportPeriodEnd", out var e) && e.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(e.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var parsedEnd))
            {
                end = parsedEnd;
            }
        }
        catch
        {
            // Fall through with whatever we have.
        }

        return (start, end);
    }

    /// <summary>
    /// Extracts the <c>selectedMeasures</c> array from the saved scenario's
    /// <c>RunConfigurationJson</c>. Quick Launch's hidden-input form doesn't carry the
    /// measures list, so without this extraction every Quick-Launch run would silently
    /// fall back to the scenario-kind default (ACH Monthly), discarding whatever the
    /// user had checked on the saved scenario.
    /// Returns null on any parse failure or when the array is missing/empty so the
    /// caller can fall back to defaults.
    /// </summary>
    private static List<ProfiledMeasureType>? ExtractSelectedMeasuresFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("selectedMeasures", out var arr)
                || arr.ValueKind != JsonValueKind.Array
                || arr.GetArrayLength() == 0)
                return null;

            var measures = new List<ProfiledMeasureType>();
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                if (Enum.TryParse<ProfiledMeasureType>(item.GetString(), ignoreCase: true, out var parsed))
                    measures.Add(parsed);
            }

            return measures.Count > 0 ? measures : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts cohort definitions from the <c>RunConfigurationJson</c> blob.
    /// The JSON is the full scenario payload produced by the UI and contains a
    /// <c>patientCohorts</c> array.  Returns null when parsing fails or the
    /// array is empty so the caller can fall back to defaults.
    /// </summary>
    private static List<PatientCohortDefinition>? ExtractCohortsFromJson(string? json, List<ProfiledMeasureType> measures)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("patientCohorts", out var cohortsEl)
                || cohortsEl.ValueKind != JsonValueKind.Array
                || cohortsEl.GetArrayLength() == 0)
                return null;

            var cohorts = new List<PatientCohortDefinition>();

            foreach (var item in cohortsEl.EnumerateArray())
            {
                var cohort = JsonSerializer.Deserialize<PatientCohortDefinition>(item.GetRawText(), CohortJsonOptions);
                if (cohort == null)
                    continue;

                foreach (var m in measures)
                    cohort.MeasureEligibilities.TryAdd(m, MeasureEligibility.Qualifying);

                var hasExplicitQualification = item.ValueKind == JsonValueKind.Object
                    && item.EnumerateObject().Any(p =>
                        string.Equals(p.Name, "cohortQualification", StringComparison.OrdinalIgnoreCase));
                if (!hasExplicitQualification)
                    cohort.CohortQualification = InferCohortQualification(item, cohort.MeasureEligibilities, measures);

                cohorts.Add(cohort);
            }

            return cohorts.Count > 0 ? cohorts : null;
        }
        catch
        {
            return null;
        }

    }



    /// <summary>
    /// Extracts an imported-patient list (<c>importedPatientIds</c> or <c>importedPatientBundles</c>)
    /// from the saved scenario's <c>RunConfigurationJson</c>. Returns null on any parse failure or
    /// when the array is missing/empty so the caller can fall back to defaults.
    /// </summary>
    private static List<ImportedPatientInput>? ExtractImportedFromJson(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty(propertyName, out var arr)
                || arr.ValueKind != JsonValueKind.Array
                || arr.GetArrayLength() == 0)
                return null;

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
            serializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

            var list = JsonSerializer.Deserialize<List<ImportedPatientInput>>(arr.GetRawText(), serializerOptions);
            return list != null && list.Count > 0 ? list : null;
        }
        catch
        {
            return null;
        }
    }

    private static MeasureEligibility InferCohortQualification(
        JsonElement cohortElement,
        IReadOnlyDictionary<ProfiledMeasureType, MeasureEligibility> eligibilities,
        IReadOnlyList<ProfiledMeasureType> measures)
    {
        if (cohortElement.TryGetProperty("cohortQualification", out var cqEl))
        {
            if (cqEl.ValueKind == JsonValueKind.String
                && Enum.TryParse<MeasureEligibility>(cqEl.GetString(), ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            if (cqEl.ValueKind == JsonValueKind.Number)
            {
                var asInt = cqEl.GetInt32();
                if (asInt == (int)MeasureEligibility.Qualifying) return MeasureEligibility.Qualifying;
                if (asInt == (int)MeasureEligibility.NonQualifying) return MeasureEligibility.NonQualifying;
            }
        }

        // Back-compat inference for scenarios saved before cohortQualification existed.
        var allNonQualifying = measures.Count > 0
            && measures.All(m => eligibilities.TryGetValue(m, out var e) && e == MeasureEligibility.NonQualifying);
        return allNonQualifying ? MeasureEligibility.NonQualifying : MeasureEligibility.Qualifying;
    }
}
