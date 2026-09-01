using Automation.UI.Models.ApiHealth;
using System.Collections.Concurrent;
using System.Reflection;

namespace Automation.UI.Services.ApiHealth.TestSuites;

public static class ApiEndPointLibrary
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<ApiEndpointDefinition>> _orderedByService = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, ApiEndpointDefinition>> _endpointByNameByService = new(StringComparer.OrdinalIgnoreCase);

    static ApiEndPointLibrary()
    {
        var services = new[]
        {
            ServiceNames.Account,
            ServiceNames.AdminBff,
            ServiceNames.AdminBffAuth,
            ServiceNames.Audit,
            ServiceNames.Census,
            ServiceNames.DataAcquisition,
            ServiceNames.Dmrp,
            ServiceNames.MeasureEval,
            ServiceNames.Normalization,
            ServiceNames.QueryDispatch,
            ServiceNames.Report,
            ServiceNames.Submission,
            ServiceNames.Tenant,
            ServiceNames.Terminology,
            ServiceNames.Validation
        };

        foreach (var service in services)
            EnsureService(service);
    }

    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ApiEndpointDefinition>> EndpointLibrary => _endpointByNameByService;

    public static IReadOnlyList<ApiEndpointDefinition> GetServiceEndpoints(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        EnsureService(serviceName);
        return _orderedByService.TryGetValue(serviceName, out var endpoints) ? endpoints : [];
    }

    public static IReadOnlyList<ApiEndpointDefinition> GetAllEndpoints() =>
        _orderedByService.Values.SelectMany(v => v).ToList();

    public static bool TryGetEndpoint(string serviceName, string endpointName, out ApiEndpointDefinition? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(endpointName))
            return false;

        EnsureService(serviceName);
        return _endpointByNameByService.TryGetValue(serviceName, out var map)
               && map.TryGetValue(endpointName, out endpoint);
    }

    private static void EnsureService(string serviceName)
    {
        if (_orderedByService.ContainsKey(serviceName) && _endpointByNameByService.ContainsKey(serviceName))
            return;

        var ordered = BuildServiceEndpoints(serviceName);
        var byName = ordered.ToDictionary(e => e.EndpointName, StringComparer.Ordinal);

        _orderedByService.TryAdd(serviceName, ordered);
        _endpointByNameByService.TryAdd(serviceName, byName);
    }

    private static IReadOnlyList<ApiEndpointDefinition> BuildServiceEndpoints(string serviceName) =>
        serviceName switch
        {
            ServiceNames.Account => BuildFromStepConstants(ServiceNames.Account, typeof(AccountSteps), BuildAccountMetadata()),
            ServiceNames.AdminBff => BuildFromStepConstants(ServiceNames.AdminBff, typeof(AdminBffSteps), BuildAdminBffMetadata()),
            ServiceNames.AdminBffAuth => BuildFromStepConstants(ServiceNames.AdminBffAuth, typeof(AdminBffAuthSteps), BuildAdminBffAuthMetadata()),
            ServiceNames.Audit => BuildFromStepConstants(ServiceNames.Audit, typeof(AuditSteps), BuildAuditMetadata()),
            ServiceNames.Census => BuildFromStepConstants(ServiceNames.Census, typeof(CensusSteps), BuildCensusMetadata()),
            ServiceNames.DataAcquisition => BuildFromStepConstants(ServiceNames.DataAcquisition, typeof(DataAcquisitionSteps), BuildDataAcquisitionMetadata()),
            ServiceNames.Dmrp => BuildFromStepConstants(ServiceNames.Dmrp, typeof(DmrpSteps), BuildDmrpMetadata()),
            ServiceNames.MeasureEval => BuildFromStepConstants(ServiceNames.MeasureEval, typeof(MeasureEvalSteps), BuildMeasureEvalMetadata()),
            ServiceNames.Normalization => BuildFromStepConstants(ServiceNames.Normalization, typeof(NormalizationSteps), BuildNormalizationMetadata()),
            ServiceNames.QueryDispatch => BuildFromStepConstants(ServiceNames.QueryDispatch, typeof(QueryDispatchSteps), BuildQueryDispatchMetadata()),
            ServiceNames.Report => BuildFromStepConstants(ServiceNames.Report, typeof(ReportSteps), BuildReportMetadata()),
            ServiceNames.Submission => BuildFromStepConstants(ServiceNames.Submission, typeof(SubmissionSteps), BuildSubmissionMetadata()),
            ServiceNames.Tenant => BuildFromStepConstants(ServiceNames.Tenant, typeof(TenantSteps), BuildTenantMetadata()),
            ServiceNames.Terminology => BuildFromStepConstants(ServiceNames.Terminology, typeof(TerminologySteps), BuildTerminologyMetadata()),
            ServiceNames.Validation => BuildFromStepConstants(ServiceNames.Validation, typeof(ValidationSteps), BuildValidationMetadata()),
            _ => []
        };

    private static IReadOnlyList<ApiEndpointDefinition> BuildFromStepConstants(
        string service,
        Type stepType,
        IReadOnlyDictionary<string, EndpointMeta>? metadata = null)
    {
        var fields = stepType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .OrderBy(f => f.MetadataToken)
            .ToList();

        var endpoints = new List<ApiEndpointDefinition>(fields.Count);
        foreach (var field in fields)
        {
            var endpointName = field.GetRawConstantValue() as string;
            if (string.IsNullOrWhiteSpace(endpointName))
                continue;

            if (metadata is not null && metadata.TryGetValue(endpointName, out var meta) && !string.IsNullOrWhiteSpace(meta.SkipReason))
            {
                endpoints.Add(SkipStep(service, endpointName, meta.Description ?? endpointName, meta.SkipReason, meta.Group));
                continue;
            }

            if (metadata is not null && metadata.TryGetValue(endpointName, out var withMeta))
            {
                endpoints.Add(Step(service, endpointName, withMeta.Description ?? endpointName, withMeta.Group));
                continue;
            }

            endpoints.Add(Step(service, endpointName, endpointName));
        }

        return endpoints;
    }

    private static IReadOnlyDictionary<string, EndpointMeta> BuildAccountMetadata() =>
    new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [AccountSteps.InfoGet200] = new EndpointMeta("Returns Account service information.", "GET /api/Account/info"),
        [AccountSteps.RootHealthGet200] = new EndpointMeta("Returns Account service health status.", "GET /health")

    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildAdminBffAuthMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [AdminBffAuthSteps.ValidBearerGet200] = new EndpointMeta("Valid Bearer token GET \u2192 200", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.TokenReuseGet200] = new EndpointMeta("Reused valid Bearer token GET \u2192 200", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.InvalidSignatureBearerGet401] = new EndpointMeta("Invalid-signature Bearer token GET \u2192 401", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.ExpiredBearerGet401Or403] = new EndpointMeta("Expired Bearer token GET \u2192 401/403", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.EmptyBearerGet401] = new EndpointMeta("Empty Bearer token GET \u2192 401", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.MalformedBearerGet401] = new EndpointMeta("Malformed Bearer token GET \u2192 401", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.MissingAuthHeaderGet401] = new EndpointMeta("Missing Authorization header GET \u2192 401", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.InvalidAuthSchemeGet401] = new EndpointMeta("Invalid auth scheme (Basic) GET \u2192 401", "GET /aggregate/reports/summaries (auth)"),
        [AdminBffAuthSteps.CrossApiTokenReuseGet401] = new EndpointMeta("Cross-API token reuse GET \u2192 401", "GET /aggregate/reports/summaries (auth)")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildAuditMetadata() =>
    new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [AuditSteps.InfoGet200] = new EndpointMeta("Returns Audit service information.", "GET /api/Audit/info"),
        [AuditSteps.RootHealthGet200] = new EndpointMeta("Returns Audit service health status.", "GET /health")

    };


    private static IReadOnlyDictionary<string, EndpointMeta> BuildAdminBffMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [AdminBffSteps.InfoGet200] = new EndpointMeta("Returns Admin BFF service information.", "GET /api/info"),
        [AdminBffSteps.HealthGet200] = new EndpointMeta("Returns Admin BFF monitor health status.", "GET /api/monitor/health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildCensusMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [CensusSteps.InfoGet200] = new EndpointMeta("Returns Census service information.", "GET /api/Census/info"),
        [CensusSteps.RootHealthGet200] = new EndpointMeta("Returns Census service health status.", "GET /health"),
        [CensusSteps.AdmittedGet200] = new EndpointMeta(CensusSteps.AdmittedGet200, "/api/census/{facilityId}/history/admitted", "Admitted census rows are produced by downstream ingestion workflows and cannot be deterministically created through Census HTTP endpoints in a self-contained health run."),
        [CensusSteps.PatientEventsGet200] = new EndpointMeta(CensusSteps.PatientEventsGet200, "/api/census/patient-events", "Patient events are created by Kafka listeners, not HTTP APIs, so a deterministic 200 fixture cannot be self-seeded in this suite."),
        [CensusSteps.PatientEventDelete202] = new EndpointMeta(CensusSteps.PatientEventDelete202, "/api/census/patient-events/{id}", "Patient events are written exclusively by the Kafka PatientListsAcquiredListener. No HTTP endpoint exists to create them, so the 202 path cannot be exercised in a self-contained health test.")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildDataAcquisitionMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [DataAcquisitionSteps.InfoGet200] = new EndpointMeta("Returns Data Acquisition service information.", "GET /api/data/info"),
        [DataAcquisitionSteps.RootHealthGet200] = new EndpointMeta("Returns Data Acquisition service health status.", "GET /health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildDmrpMetadata()
    {
        const string mappings = "/api/dmrp/measure-mappings";
        const string plans = "/api/dmrp/reporting-plans";

        return new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
        {
            [DmrpSteps.MappingPost201] = new EndpointMeta("Creates a measure mapping.", $"POST {mappings}"),
            [DmrpSteps.MappingPost400UnknownDqm] = new EndpointMeta("Refuses a dQM MeasureEval does not know.", $"POST {mappings}"),
            [DmrpSteps.MappingPost400Duplicate] = new EndpointMeta("Refuses a second mapping for the same measure and dQM.", $"POST {mappings}"),
            [DmrpSteps.MappingGet200] = new EndpointMeta("Reads a measure mapping by id.", $"GET {mappings}/{{id}}"),
            [DmrpSteps.MappingGet404] = new EndpointMeta("Answers not-found for an unknown id.", $"GET {mappings}/{{id}}"),
            [DmrpSteps.MappingSearch200] = new EndpointMeta("Finds a mapping by measure and dQM.", $"GET {mappings}/search"),
            [DmrpSteps.MappingSearch204] = new EndpointMeta("Answers no-content, not an empty page, when nothing matches.", $"GET {mappings}/search"),
            [DmrpSteps.MappingPut202] = new EndpointMeta("Updates a measure mapping.", $"PUT {mappings}/{{id}}"),
            [DmrpSteps.MappingPut404] = new EndpointMeta("Refuses to update a mapping that does not exist.", $"PUT {mappings}/{{id}}"),
            [DmrpSteps.PlanPost201] = new EndpointMeta("Enrolls a facility in a measure for a reporting period.", $"POST {plans}"),
            [DmrpSteps.PlanPost409Duplicate] = new EndpointMeta("Refuses a second plan for the same facility, mapping and period.", $"POST {plans}"),
            [DmrpSteps.PlanPost400UnknownFacility] = new EndpointMeta("Refuses a plan for a facility that does not exist.", $"POST {plans}"),
            [DmrpSteps.PlanPost400UnknownMapping] = new EndpointMeta("Refuses a plan for a measure mapping that does not exist.", $"POST {plans}"),
            [DmrpSteps.PlanGet200] = new EndpointMeta("Reads a reporting plan by id.", $"GET {plans}/{{id}}"),
            [DmrpSteps.PlanGet404] = new EndpointMeta("Answers not-found for an unknown id.", $"GET {plans}/{{id}}"),
            [DmrpSteps.PlansForFacilityGet200] = new EndpointMeta("Lists a facility's reporting plans.", $"GET {plans}/facilities/{{facilityId}}"),
            [DmrpSteps.MappingDelete409InUse] = new EndpointMeta("Refuses to delete a mapping a reporting plan still references.", $"DELETE {mappings}/{{id}}"),
            [DmrpSteps.PlanDelete204] = new EndpointMeta("Deletes a reporting plan.", $"DELETE {plans}/{{id}}"),
            [DmrpSteps.PlanDelete404] = new EndpointMeta("Answers not-found when deleting a plan twice.", "DELETE /api/dmrp/reporting-plans/{id}"),
            [DmrpSteps.MappingDelete204] = new EndpointMeta("Deletes a mapping once nothing references it.", $"DELETE {mappings}/{{id}}"),
            [DmrpSteps.MappingDelete404] = new EndpointMeta("Answers not-found when deleting a mapping twice.", $"DELETE {mappings}/{{id}}"),
            [DmrpSteps.FacilityPost400WithSchedule] = new EndpointMeta(
                "Refuses a facility that carries its own schedule while DMRP is enabled; the schedule is derived from reporting plans.",
                "POST /api/Facility")
        };
    }

    private static IReadOnlyDictionary<string, EndpointMeta> BuildMeasureEvalMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [MeasureEvalSteps.InfoGet200] = new EndpointMeta("Returns MeasureEval service information.", "GET /api/measureeval/info"),
        [MeasureEvalSteps.RootHealthGet200] = new EndpointMeta("Returns MeasureEval service health status.", "GET /health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildNormalizationMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [NormalizationSteps.InfoGet200] = new EndpointMeta("Returns Normalization service information.", "GET /api/Normalization/info"),
        [NormalizationSteps.RootHealthGet200] = new EndpointMeta("Returns Normalization service health status.", "GET /health"),
        [NormalizationSteps.LocationPost201] = new EndpointMeta("Creates a facility location.", "POST /api/normalization/facility-locations/facilities/{facilityId}/locations"),
        [NormalizationSteps.LocationPost400EmptyLocationId] = new EndpointMeta("Rejects a facility location with an empty location id.", "POST /api/normalization/facility-locations/facilities/{facilityId}/locations"),
        [NormalizationSteps.LocationPost409Duplicate] = new EndpointMeta("Rejects a duplicate facility location.", "POST /api/normalization/facility-locations/facilities/{facilityId}/locations"),
        [NormalizationSteps.LocationGet200] = new EndpointMeta("Returns a facility location including LocationName and LocationAlias.", "GET /api/normalization/facility-locations/facilities/{facilityId}/locations/{locationId}"),
        [NormalizationSteps.LocationGet400EmptyLocationId] = new EndpointMeta("Rejects a facility-location get with an empty location id.", "GET /api/normalization/facility-locations/facilities/{facilityId}/locations/{locationId}"),
        [NormalizationSteps.LocationGet404] = new EndpointMeta("Returns 404 for a facility location that does not exist.", "GET /api/normalization/facility-locations/facilities/{facilityId}/locations/{locationId}"),
        [NormalizationSteps.MappingPost201] = new EndpointMeta("Creates an HSLOC mapping including LocalCodeSystem.", "POST /api/normalization/hsloc-mappings/facilities/{facilityId}"),
        [NormalizationSteps.MappingPost400EmptyLocalCode] = new EndpointMeta("Rejects an HSLOC mapping with an empty local code.", "POST /api/normalization/hsloc-mappings/facilities/{facilityId}"),
        [NormalizationSteps.MappingPost404UnknownLocation] = new EndpointMeta("Returns 404 when the facility location does not exist.", "POST /api/normalization/hsloc-mappings/facilities/{facilityId}"),
        [NormalizationSteps.MappingPost409Duplicate] = new EndpointMeta("Rejects a duplicate HSLOC mapping.", "POST /api/normalization/hsloc-mappings/facilities/{facilityId}"),
        [NormalizationSteps.MappingGet200] = new EndpointMeta("Returns an HSLOC mapping including LocalCodeSystem.", "GET /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingGet400EmptyId] = new EndpointMeta("Rejects an HSLOC mapping get with an empty id.", "GET /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingGet404] = new EndpointMeta("Returns 404 for an HSLOC mapping that does not exist.", "GET /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingSearch200HasResults] = new EndpointMeta("Searches HSLOC mappings and returns the created row.", "GET /api/normalization/hsloc-mappings/search"),
        [NormalizationSteps.MappingSearch200Empty] = new EndpointMeta("Searches HSLOC mappings and returns an empty page.", "GET /api/normalization/hsloc-mappings/search"),
        [NormalizationSteps.MappingPut202] = new EndpointMeta("Updates an HSLOC mapping.", "PUT /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingPut400EmptyLocalCode] = new EndpointMeta("Rejects an HSLOC mapping update with an empty local code.", "PUT /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingPut404] = new EndpointMeta("Returns 404 when updating an HSLOC mapping that does not exist.", "PUT /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingPut409Duplicate] = new EndpointMeta("Rejects an HSLOC mapping update that collides with an existing local code.", "PUT /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingDelete204] = new EndpointMeta("Deletes an HSLOC mapping by id.", "DELETE /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingDelete400EmptyId] = new EndpointMeta("Rejects an HSLOC mapping delete with an empty id.", "DELETE /api/normalization/hsloc-mappings/{mappingId}"),
        [NormalizationSteps.MappingDeleteFacility204] = new EndpointMeta("Deletes HSLOC mappings for a facility.", "DELETE /api/normalization/hsloc-mappings/facilities/{facilityId}"),
        [NormalizationSteps.MappingDeleteFacility400EmptyFacility] = new EndpointMeta("Rejects a facility mapping delete with an empty facility id.", "DELETE /api/normalization/hsloc-mappings/facilities/{facilityId}")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildQueryDispatchMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [QueryDispatchSteps.InfoGet200] = new EndpointMeta("Returns QueryDispatch service information.", "GET /api/QueryDispatch/info"),
        [QueryDispatchSteps.RootHealthGet200] = new EndpointMeta("Returns QueryDispatch service health status.", "GET /health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildReportMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [ReportSteps.InfoGet200] = new EndpointMeta("Returns Report service information.", "GET /api/Report/info"),
        [ReportSteps.RootHealthGet200] = new EndpointMeta("Returns Report service health status.", "GET /health"),
        [ReportSteps.ResourceGet200HasData] = new EndpointMeta(ReportSteps.ResourceGet200HasData, "GET /api/resources/{id}", "Resource rows are intentionally not persisted for seeded runs in this environment, so no deterministic resource id exists.")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildSubmissionMetadata() =>
    new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [SubmissionSteps.InfoGet200] = new EndpointMeta("Returns Submission service information.", "GET /api/Submission/info"),
        [SubmissionSteps.RootHealthGet200] = new EndpointMeta("Returns Submission service health status.", "GET /health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildTenantMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [TenantSteps.InfoGet200] = new EndpointMeta("Returns Tenant service information.", "GET /api/facility/info"),
        [TenantSteps.RootHealthGet200] = new EndpointMeta("Returns Tenant service health status.", "GET /health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildTerminologyMetadata() =>
    new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [TerminologySteps.InfoGet200] = new EndpointMeta("Returns Terminology service information.", "GET /api/Terminology/info"),
        [TerminologySteps.RootHealthGet200] = new EndpointMeta("Returns Terminology service health status.", "GET /health")
    };

    private static IReadOnlyDictionary<string, EndpointMeta> BuildValidationMetadata() => new Dictionary<string, EndpointMeta>(StringComparer.Ordinal)
    {
        [ValidationSteps.InfoGet200] = new EndpointMeta("Returns Validation service information.", "GET /api/validation/info"),
        [ValidationSteps.RootHealthGet200] = new EndpointMeta("Returns Validation service health status.", "GET /health")
    };

    private sealed record EndpointMeta(string? Description, string? Group, string? SkipReason = null);

    private static ApiEndpointDefinition Step(string service, string name, string desc, string? group = null) => new()
    {
        ServiceName = service,
        GroupName = group,
        EndpointName = name,
        Description = desc,
        IsTestSuiteStep = true
    };

    private static ApiEndpointDefinition SkipStep(string service, string name, string desc, string skipReason, string? group = null) => new()
    {
        ServiceName = service,
        GroupName = group,
        EndpointName = name,
        Description = desc,
        AlwaysSkipReason = skipReason,
        IsTestSuiteStep = true
    };

    public static class ServiceNames
    {
        public const string Account = "Account";
        public const string AdminBff = "AdminBff";
        public const string AdminBffAuth = "AdminBffAuth";
        public const string Audit = "Audit";
        public const string Census = "Census";
        public const string DataAcquisition = "DataAcquisition";
        public const string Dmrp = "DMRP";
        public const string MeasureEval = "MeasureEval";
        public const string Normalization = "Normalization";
        public const string QueryDispatch = "QueryDispatch";
        public const string Report = "Report";
        public const string Submission = "Submission";
        public const string Tenant = "Tenant";
        public const string Terminology = "Terminology";
        public const string Validation = "Validation";
    }

    public static class AdminBffSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string HealthGet200 = "Health GET → 200";
        public const string FacilityDelete200 = "Facility DELETE → 200";
        public const string FacilityDelete404 = "Facility DELETE → 404";
        public const string FacilityRestorePatch200 = "Facility Restore PATCH → 200";
        public const string FacilityRestorePatch404 = "Facility Restore PATCH → 404";
        public const string SummariesGet200 = "Summaries GET → 200";
        public const string SummaryGet200 = "Summary GET → 200";
        public const string SummaryGet404 = "Summary GET → 404";
        public const string ReportDelete204 = "Report DELETE → 204";
        public const string ReportDelete404 = "Report DELETE → 404";
        public const string ReportRestorePatch204 = "Report Restore PATCH → 204";
        public const string ReportRestorePatch404 = "Report Restore PATCH → 404";
    }

    public static class AccountSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
    }

    public static class AdminBffAuthSteps
    {
        public const string ValidBearerGet200 = "Valid credentials/token → access granted";
        public const string TokenReuseGet200 = "Valid token reuse → access granted";
        public const string InvalidSignatureBearerGet401 = "Invalid credentials/token → access denied (401)";
        public const string ExpiredBearerGet401Or403 = "Expired credentials/token → access denied (401/403)";
        public const string EmptyBearerGet401 = "Missing access token (Bearer empty) → 401";
        public const string MalformedBearerGet401 = "Invalid access token (malformed) → 401";
        public const string MissingAuthHeaderGet401 = "Missing auth header → 401";
        public const string InvalidAuthSchemeGet401 = "Invalid auth scheme (Basic) → 401";
        public const string CrossApiTokenReuseGet401 = "Cross-API token reuse → 401";
    }

    public static class AuditSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
    }

    public static class CensusSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string ConfigPost201 = "Config POST → 201";
        public const string ConfigPost400EmptyScheduledTrigger = "Config POST → 400 (empty ScheduledTrigger)";
        public const string ConfigPost400EmptyFacilityId = "Config POST → 400 (empty FacilityId)";
        public const string ConfigPost400InvalidCron = "Config POST → 400 (invalid cron)";
        public const string ConfigGet200 = "Config GET → 200";
        public const string ConfigGet404 = "Config GET → 404";
        public const string ConfigPut202 = "Config PUT → 202";
        public const string ConfigPut400MismatchedFacilityId = "Config PUT → 400 (mismatched FacilityId)";
        public const string ConfigPut400EmptyFacilityId = "Config PUT → 400 (empty FacilityId)";
        public const string ConfigPut400EmptyScheduledTrigger = "Config PUT → 400 (empty ScheduledTrigger)";
        public const string ConfigPut400InvalidCron = "Config PUT → 400 (invalid cron)";
        public const string ConfigDelete202 = "Config DELETE → 202";
        public const string DisableJobsDelete204 = "DisableJobs DELETE → 204";
        public const string DisableJobsDelete400 = "DisableJobs DELETE → 400";
        public const string EnableJobsPatch204 = "EnableJobs PATCH → 204";
        public const string EnableJobsPatch400 = "EnableJobs PATCH → 400";
        public const string AdmittedGet200 = "Admitted GET → 200";
        public const string AdmittedGet404 = "Admitted GET → 404";
        public const string AdmittedGet400 = "Admitted GET → 400";
        public const string CurrentEncountersGet200 = "CurrentEncounters GET → 200";
        public const string CurrentEncountersGet400 = "CurrentEncounters GET → 400";
        public const string HistoricalEncountersGet200 = "HistoricalEncounters GET → 200";
        public const string HistoricalEncountersGet400 = "HistoricalEncounters GET → 400";
        public const string PatientEventsGet200 = "PatientEvents GET → 200";
        public const string PatientEventsGet404 = "PatientEvents GET → 404";
        public const string PatientEventsGet400 = "PatientEvents GET → 400";
        public const string PatientEventDelete400EmptyId = "PatientEvent DELETE → 400 (empty id)";
        public const string PatientEventDelete404NonExistent = "PatientEvent DELETE → 404 (non-existent)";
        public const string PatientEventDelete202 = "PatientEvent DELETE → 202";
        public const string PatientEventByCorrelationDelete400 = "PatientEventByCorrelation DELETE → 400";
        public const string PatientEventByCorrelationDelete202 = "PatientEventByCorrelation DELETE → 202";
        public const string RebuildPost202 = "Rebuild POST → 202";
        public const string RebuildPost400 = "Rebuild POST → 400";
    }

    public static class DataAcquisitionSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string FhirConfigPost201 = "FhirConfig POST → 201";
        public const string FhirConfigPost409 = "FhirConfig POST → 409";
        public const string FhirConfigGet200 = "FhirConfig GET → 200";
        public const string FhirConfigGet404 = "FhirConfig GET → 404";
        public const string FhirConfigDelete202 = "FhirConfig DELETE → 202";
        public const string FhirListConfigPost200 = "FhirListConfig POST → 200";
        public const string FhirListConfigGet200 = "FhirListConfig GET → 200";
        public const string FhirListConfigPost409 = "FhirListConfig POST → 409";
        public const string FhirListConfigDelete200 = "FhirListConfig DELETE → 200";
        public const string FhirListConfigGet404 = "FhirListConfig GET → 404";
        public const string QueryPlanPost201 = "QueryPlan POST → 201";
        public const string QueryPlanPost409 = "QueryPlan POST → 409";
        public const string QueryPlanGet200 = "QueryPlan GET → 200";
        public const string QueryPlanGet404 = "QueryPlan GET → 404";
        public const string QueryPlanDelete202 = "QueryPlan DELETE → 202";
        public const string OrgLocationConfigGet200 = "OrgLocationConfig GET → 200";
        public const string OrgLocationConfigPost201 = "OrgLocationConfig POST → 201";
        public const string OrgLocationConfigGet200AfterPost = "OrgLocationConfig GET → 200 (after post)";
        public const string OrgLocationMappingsGet200 = "OrgLocationMappings GET → 200";
        public const string EncounterMappingsGet200 = "EncounterMappings GET → 200";
        public const string AcquisitionLogsGet200HasData = "AcquisitionLogs GET → 200 (has data)";
        public const string AcquisitionLogsGet200 = "AcquisitionLogs GET → 200";
        public const string GetLog200HasData = "GetLog → 200 (has data)";
        public const string GetLog400Id0 = "GetLog → 400 (id=0)";
        public const string GetLog404NonExistent = "GetLog → 404 (non-existent)";
        public const string GetNotes200HasData = "GetNotes → 200 (has data)";
        public const string GetNotes400Id0 = "GetNotes → 400 (id=0)";
        public const string GetNotes200NonExistent = "GetNotes → 200 (non-existent)";
        public const string GetRefs200HasData = "GetRefs → 200 (has data)";
        public const string GetRefs400Id0 = "GetRefs → 400 (id=0)";
        public const string GetRefs200NonExistent = "GetRefs → 200 (non-existent)";
        public const string Process202HasData = "Process → 202 (has data)";
        public const string Process400Id0 = "Process → 400 (id=0)";
        public const string Process404NonExistent = "Process → 404 (non-existent)";
        public const string ProcessBulk202 = "ProcessBulk → 202";
        public const string ProcessBulk400Empty = "ProcessBulk → 400 (empty)";
        public const string CancelBulk202 = "CancelBulk → 202";
        public const string CancelBulk400Empty = "CancelBulk → 400 (empty)";
        public const string CancelBulk400NegHours = "CancelBulk → 400 (neg hours)";
        public const string ProcessFilter202 = "ProcessFilter → 202";
        public const string ProcessFilter400NoCriteria = "ProcessFilter → 400 (no criteria)";
        public const string CancelFilter202 = "CancelFilter → 202";
        public const string CancelFilter400NoCriteria = "CancelFilter → 400 (no criteria)";
        public const string CancelFilter400NegHours = "CancelFilter → 400 (neg hours)";
        public const string DeleteLog400Id0 = "DeleteLog → 400 (id=0)";
        public const string DeleteLog404NonExistent = "DeleteLog → 404 (non-existent)";
        public const string SoftDeleteFacility204 = "SoftDeleteFacility → 204";
        public const string SoftDeleteReport204 = "SoftDeleteReport → 204";
        public const string RestoreReport204 = "RestoreReport → 204";
        public const string RestoreFacility200 = "RestoreFacility → 200";
        public const string StatusCountsGet200HasData = "StatusCounts GET → 200 (has data)";
        public const string StatusCountsGet200 = "StatusCounts GET → 200";
        public const string SummaryGet200HasData = "Summary GET → 200 (has data)";
        public const string SummaryGet200 = "Summary GET → 200";
        public const string ResourceIdsGet200HasData = "ResourceIds GET → 200 (has data)";
        public const string ResourceIdsGet200 = "ResourceIds GET → 200";
        public const string ResourceIds400NoFacility = "ResourceIds → 400 (no facility)";
        public const string Search400BadSortBy = "Search → 400 (bad sortBy)";
    }

    public static class NormalizationSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string Post201 = "POST → 201";
        public const string Post400InvalidOperationType = "POST → 400 (invalid operation type)";
        public const string Post400EmptyResourceTypes = "POST → 400 (empty resourceTypes)";
        public const string Get200HasResults = "GET → 200 (has results)";
        public const string Get200Empty = "GET → 200 (empty)";
        public const string Get400BadFacility = "GET → 400 (bad facility)";
        public const string Get200Sequences = "GET → 200 (sequences)";
        public const string Get400SequencesBadFacility = "GET → 400 (sequences bad facility)";
        public const string SequencesPost201 = "SEQUENCES POST → 201";
        public const string SequencesPost400EmptyFacility = "SEQUENCES POST → 400 (empty facility)";
        public const string SequencesPost400EmptyBody = "SEQUENCES POST → 400 (empty body)";
        public const string Delete204 = "DELETE → 204";
        public const string Delete404NoRecords = "DELETE → 404 (no records)";
        public const string SequencesDelete204 = "SEQUENCES DELETE → 204";
        public const string SequencesDelete404 = "SEQUENCES DELETE → 404";
        public const string SequencesDelete400EmptyFacility = "SEQUENCES DELETE → 400 (empty facility)";
        public const string LocationPost201 = "LOCATION POST → 201";
        public const string LocationPost400EmptyLocationId = "LOCATION POST → 400 (empty locationId)";
        public const string LocationPost409Duplicate = "LOCATION POST → 409 (duplicate)";
        public const string LocationGet200 = "LOCATION GET → 200";
        public const string LocationGet400EmptyLocationId = "LOCATION GET → 400 (empty locationId)";
        public const string LocationGet404 = "LOCATION GET → 404";
        public const string MappingPost201 = "MAPPING POST → 201";
        public const string MappingPost400EmptyLocalCode = "MAPPING POST → 400 (empty localCode)";
        public const string MappingPost404UnknownLocation = "MAPPING POST → 404 (unknown location)";
        public const string MappingPost409Duplicate = "MAPPING POST → 409 (duplicate)";
        public const string MappingGet200 = "MAPPING GET → 200";
        public const string MappingGet400EmptyId = "MAPPING GET → 400 (empty id)";
        public const string MappingGet404 = "MAPPING GET → 404";
        public const string MappingSearch200HasResults = "MAPPING SEARCH → 200 (has results)";
        public const string MappingSearch200Empty = "MAPPING SEARCH → 200 (empty)";
        public const string MappingPut202 = "MAPPING PUT → 202";
        public const string MappingPut400EmptyLocalCode = "MAPPING PUT → 400 (empty localCode)";
        public const string MappingPut404 = "MAPPING PUT → 404";
        public const string MappingPut409Duplicate = "MAPPING PUT → 409 (duplicate)";
        public const string MappingDelete204 = "MAPPING DELETE → 204";
        public const string MappingDelete400EmptyId = "MAPPING DELETE → 400 (empty id)";
        public const string MappingDeleteFacility204 = "MAPPING DELETE FACILITY → 204";
        public const string MappingDeleteFacility400EmptyFacility = "MAPPING DELETE FACILITY → 400 (empty facility)";
    }

    public static class QueryDispatchSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string Post400NullModel = "POST → 400 (empty model)";
        public const string Post400EmptyFacilityId = "POST → 400 (empty facilityId)";
        public const string Post400InvalidDuration = "POST → 400 (invalid duration)";
        public const string Post400FacilityNotFound = "POST → 400 (facility not found)";
        public const string Post201 = "POST → 201";
        public const string Post400AlreadyExists = "POST → 400 (already exists)";
        public const string Put400NullModel = "PUT → 400 (null model)";
        public const string Put400EmptyFacilityId = "PUT → 400 (empty facilityId)";
        public const string Put400InvalidDuration = "PUT → 400 (invalid duration)";
        public const string Put201Or204 = "PUT → 201/204";
        public const string Get200 = "GET → 200";
        public const string Get404 = "GET → 404";
        public const string Delete204 = "DELETE → 204";
        public const string Delete204Idempotent = "DELETE → 204 (idempotent)";
    }

    public static class ReportSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string GetSchedule200HasData = "Get Schedule → 200 (has data)";
        public const string GetSchedule404 = "Get Schedule → 404";
        public const string GetSchedule400BadGuid = "Get Schedule → 400 (bad guid)";
        public const string GetByFacility400Empty = "Get By Facility → 400 (empty)";
        public const string GetByFacility200Empty = "Get By Facility → 200 (empty)";
        public const string GetByFacility200HasData = "Get By Facility → 200 (has data)";
        public const string Search200 = "Search → 200";
        public const string SoftDelete400BadGuid = "SoftDelete → 400 (bad guid)";
        public const string SoftDelete404 = "SoftDelete → 404";
        public const string SoftDelete204 = "SoftDelete → 204";
        public const string Restore400BadGuid = "Restore → 400 (bad guid)";
        public const string Restore404 = "Restore → 404";
        public const string Restore204 = "Restore → 204";
        public const string SetStatus204 = "SetStatus → 204";
        public const string EntryGet200HasData = "Entry GET → 200 (has data)";
        public const string EntryGet404 = "Entry GET → 404";
        public const string EntryGet400BadGuid = "Entry GET → 400 (bad guid)";
        public const string Entries200HasData = "Entries → 200 (has data)";
        public const string Entries404 = "Entries → 404";
        public const string Entries400EmptyId = "Entries → 400 (empty id)";
        public const string Entries400BadGuid = "Entries → 400 (bad guid)";
        public const string EntryCount200HasData = "Entry Count → 200 (has data)";
        public const string EntryCount404 = "Entry Count → 404";
        public const string EntryCount400BadGuid = "Entry Count → 400 (bad guid)";
        public const string EntrySummary200HasData = "Entry Summary → 200 (has data)";
        public const string EntrySummary404 = "Entry Summary → 404";
        public const string EntrySummary400BadGuid = "Entry Summary → 400 (bad guid)";
        public const string EntryByPatient200HasData = "Entry By Patient → 200 (has data)";
        public const string EntryByPatient404 = "Entry By Patient → 404";
        public const string EntryByPatient400BadGuid = "Entry By Patient → 400 (bad guid)";
        public const string EntriesByPatient200HasData = "Entries By Patient → 200 (has data)";
        public const string EntriesByPatient404 = "Entries By Patient → 404";
        public const string ResourceGet200HasData = "Resource GET → 200 (has data)";
        public const string ResourceGet404 = "Resource GET → 404";
        public const string ResourceGet400BadGuid = "Resource GET → 400 (bad guid)";
        public const string ResourcesBySchedule200HasData = "Resources By Schedule → 200 (has data)";
        public const string ResourcesBySchedule400BadGuid = "Resources By Schedule → 400 (bad guid)";
        public const string ResourcesBySchedulePatient200HasData = "Resources By Schedule+Patient → 200 (has data)";
        public const string ResourcesBySchedulePatient400BadGuid = "Resources By Schedule+Patient → 400 (bad guid)";
        public const string ResourcesByPatient200HasData = "Resources By Patient → 200 (has data)";
        public const string SearchResources200HasData = "Search Resources → 200 (has data)";
        public const string SearchResources200 = "Search Resources → 200";
        public const string PopulationGet200HasData = "Population GET → 200 (has data)";
        public const string PopulationGet404 = "Population GET → 404";
        public const string PopulationGet400BadGuid = "Population GET → 400 (bad guid)";
        public const string Populations400EmptyId = "Populations → 400 (empty id)";
        public const string Populations200HasData = "Populations → 200 (has data)";
        public const string Populations200 = "Populations → 200";
        public const string Populations400BadGuid = "Populations → 400 (bad guid)";
        public const string InitPopCount200HasData = "InitPop Count → 200 (has data)";
        public const string InitPopCount200 = "InitPop Count → 200";
        public const string InitPopCount400EmptyId = "InitPop Count → 400 (empty id)";
        public const string InitPopCount400BadGuid = "InitPop Count → 400 (bad guid)";
    }

    public static class TenantSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string Create201 = "Create → 201";
        public const string Create400Duplicate = "Create → 400 (duplicate)";
        public const string Create400NoName = "Create → 400 (no name)";
        public const string Search200 = "Search → 200";
        public const string Search204NoResults = "Search → 204 (no results)";
        public const string List200 = "List → 200";
        public const string List204 = "List → 204";
        public const string Get200 = "Get → 200";
        public const string Get404 = "Get → 404";
        public const string Update200 = "Update → 200";
        public const string Update404NonExistent = "Update → 404 (non-existent)";
        public const string CheckExists200 = "CheckExists → 200";
        public const string CheckExists404 = "CheckExists → 404";
        public const string SoftDelete204 = "SoftDelete → 204";
        public const string SoftDelete204Idempotent = "SoftDelete → 204 (idempotent)";
        public const string SoftDelete404NonExistent = "SoftDelete → 404 (non-existent)";
        public const string Restore204 = "Restore → 204";
        public const string Restore400NotDeleted = "Restore → 400 (not deleted)";
        public const string Restore404NonExistent = "Restore → 404 (non-existent)";
        public const string VendorPost201 = "Vendor POST → 201";
        public const string VendorPost409 = "Vendor POST → 409";
        public const string VendorGet200 = "Vendor GET → 200";
        public const string VendorsGet200 = "Vendors GET → 200";
        public const string VendorDelete204 = "Vendor DELETE → 204";
        public const string Delete204 = "Delete → 204";
        public const string Delete404NonExistent = "Delete → 404 (non-existent)";
        public const string AdHoc200Seeded = "AdHocReport → 200 (seeded)";
        public const string AdHoc400NoMeasures = "AdHocReport → 400 (no measures)";
        public const string AdHoc400NoStartDate = "AdHocReport → 400 (no start date)";
        public const string AdHoc400NoEndDate = "AdHocReport → 400 (no end date)";
        public const string AdHoc400EndBeforeStart = "AdHocReport → 400 (end before start)";
        public const string AdHoc404NonExistent = "AdHocReport → 404 (non-existent)";
        public const string Regenerate200Seeded = "RegenerateReport → 200 (seeded)";
        public const string Regenerate400NoReportId = "RegenerateReport → 400 (no report id)";
        public const string Regenerate404NonExistentFacility = "RegenerateReport → 404 (non-existent facility)";
        public const string Regenerate404NonExistentReport = "RegenerateReport → 404 (non-existent report)";
    }

    public static class TerminologySteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
    }

    /// <summary>
    /// DMRP is a module hosted by the Tenant service, so these run against the Tenant base address.
    /// Order matters: the delete steps depend on what the create steps left behind.
    /// </summary>
    public static class DmrpSteps
    {
        public const string MappingPost201 = "MeasureMapping POST → 201";
        public const string MappingPost400UnknownDqm = "MeasureMapping POST → 400 (dQM not in MeasureEval)";
        public const string MappingPost400Duplicate = "MeasureMapping POST → 400 (duplicate measure + dQM)";
        public const string MappingGet200 = "MeasureMapping GET → 200";
        public const string MappingGet404 = "MeasureMapping GET → 404";
        public const string MappingSearch200 = "MeasureMappings SEARCH → 200 (filtered)";
        public const string MappingSearch204 = "MeasureMappings SEARCH → 204 (no match)";
        public const string MappingPut202 = "MeasureMapping PUT → 202";
        public const string MappingPut404 = "MeasureMapping PUT → 404 (non-existent)";
        public const string PlanPost201 = "ReportingPlan POST → 201";
        public const string PlanPost409Duplicate = "ReportingPlan POST → 409 (duplicate period)";
        public const string PlanPost400UnknownFacility = "ReportingPlan POST → 400 (non-existent facility)";
        public const string PlanPost400UnknownMapping = "ReportingPlan POST → 400 (non-existent measure mapping)";
        public const string PlanGet200 = "ReportingPlan GET → 200";
        public const string PlanGet404 = "ReportingPlan GET → 404";
        public const string PlansForFacilityGet200 = "ReportingPlans for facility GET → 200";
        public const string MappingDelete409InUse = "MeasureMapping DELETE → 409 (referenced by a reporting plan)";
        public const string PlanDelete204 = "ReportingPlan DELETE → 204";
        public const string PlanDelete404 = "ReportingPlan DELETE → 404";
        public const string MappingDelete204 = "MeasureMapping DELETE → 204";
        public const string MappingDelete404 = "MeasureMapping DELETE → 404";
        public const string FacilityPost400WithSchedule = "Facility POST → 400 (schedule supplied while DMRP is enabled)";
    }

    public static class MeasureEvalSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string GetAll200 = "GET ALL → 200";
        public const string Get200 = "GET → 200";
        public const string Get404 = "GET → 404";
        public const string Put400MalformedBody = "PUT → 400 (malformed body)";
        public const string Put400NoMeasureInBundle = "PUT → 400 (no measure in bundle)";
    }

    public static class SubmissionSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string Get200 = "GET → 200";
        public const string Get400BadReportId = "GET → 400 (bad reportId)";
        public const string Get404NotFound = "GET → 404 (not found)";
        public const string Get400EmptyFacilityId = "GET → 400 (empty facilityId)";
        public const string Get400EmptyReportId = "GET → 400 (empty reportId)";
    }

    public static class ValidationSteps
    {
        public const string InfoGet200 = "Service Info GET → 200";
        public const string RootHealthGet200 = "Root Health GET → 200";
        public const string ArtifactsGet200 = "Artifacts GET → 200";
        public const string CategoriesGet200 = "Categories GET → 200";
        public const string ArtifactPut200Or201 = "Artifact PUT → 200/201";
        public const string ResultsGet200Seeded = "Results GET → 200 (seeded)";
        public const string ResultsGet200Empty = "Results GET → 200 (empty)";
    }
}