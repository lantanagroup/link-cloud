using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class ApiStabilityTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private readonly TestScenarioConfig _config = TestConfig.BuildScenarioConfig(
        "API_STABILITY_TEST",
        defaultPatientIds: []);

    private readonly TestServices _b;
    private readonly string _facilityId = $"ApiStabilityTest-{Guid.NewGuid():N}";

    private bool _facilityCreated;
    private bool _queryConfigCreated;
    private bool _dischargePlanCreated;
    private bool _monthlyPlanCreated;
    private bool _normalizationCreated;
    private bool _censusConfigCreated;

    public ApiStabilityTest(BackendE2ETestFixture fixture)
    {
        _b = fixture.GetTestServices();
        _config.RemoveFacilityConfig = true;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!_config.RemoveFacilityConfig)
            return;

        if (_monthlyPlanCreated)
            await Try(() => _b.DataAcquisitionClient.DeleteQueryPlanAsync(_facilityId, "Monthly"));
        if (_dischargePlanCreated)
            await Try(() => _b.DataAcquisitionClient.DeleteQueryPlanAsync(_facilityId, "Discharge"));
        if (_queryConfigCreated)
            await Try(() => _b.DataAcquisitionClient.DeleteFhirQueryConfigurationAsync(_facilityId));
        if (_normalizationCreated)
            await Try(() => _b.NormalizationClient.DeleteFacilityOperationsAsync(_facilityId));
        if (_censusConfigCreated)
            await Try(() => _b.CensusClient.DeleteCensusConfigAsync(_facilityId));
        if (_facilityCreated)
            await Try(() => _b.FacilityClient.DeleteAsync(_facilityId));
    }

    [Fact]
    [Trait("Category", "ApiStabilityTest")]
    public async Task ExecuteApiStabilityTest()
    {
        var results = new ApiRunResults(_b.Output);

        await RunWithRetryAsync(results, "Validation.InitializeArtifacts",
            () => _b.SdkValidationClient.InitializeArtifactsAsync(),
            timeout: TimeSpan.FromMinutes(3), retryDelay: TimeSpan.FromSeconds(10));

        await RunWithRetryAsync(results, "Validation.InitializeCategories",
            () => _b.SdkValidationClient.InitializeCategoriesAsync(),
            timeout: TimeSpan.FromMinutes(3), retryDelay: TimeSpan.FromSeconds(10));

        string? measureId = null;
        await RunAsync(results, "MeasureLoader.Load", async () =>
        {
            var measureLoader = new MeasureLoader(_b.MeasureEvalClient, _b.SdkValidationClient, _b.Output, _config);
            await measureLoader.LoadAsync();
            measureId = measureLoader.MeasureId;
            if (string.IsNullOrWhiteSpace(measureId))
                throw new InvalidOperationException("MeasureLoader did not produce a MeasureId.");
        });

        if (string.IsNullOrWhiteSpace(measureId))
        {
            results.AddError("MeasureLoader.MeasureId", "Skipping measure-dependent API calls.");
            Assert.False(results.Errors.Any(), results.ToAssertionMessage());
            return;
        }

        await RunAsync(results, "MeasureEval.GetMeasureDefinition",
            () => _b.MeasureEvalClient.GetMeasureDefinitionAsync(measureId));

        await RunAsync(results, "Validation.UpsertResourceArtifact", async () =>
        {
            var artifactId = $"OperationOutcome-{Guid.NewGuid():N}";
            var payload = $"{{\"resourceType\":\"OperationOutcome\",\"id\":\"{Guid.NewGuid():N}\",\"issue\":[{{\"severity\":\"information\",\"code\":\"informational\",\"diagnostics\":\"ApiStabilityTest artifact\"}}]}}";
            await _b.SdkValidationClient.UpsertResourceArtifactAsync(artifactId, payload);
        });

        var facilityBody = new FacilityModel
        {
            FacilityId = _facilityId,
            FacilityName = _facilityId,
            TimeZone = "America/Chicago",
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = [measureId],
                Daily = [],
                Weekly = []
            }
        };

        await RunAsync(results, "Facility.Create", async () =>
        {
            await _b.FacilityClient.CreateAsync(facilityBody);
            _facilityCreated = true;
        });

        await RunAsync(results, "Facility.Get",
            () => _b.FacilityClient.GetAsync(_facilityId));

        var queryConfigRequest = new CreateFhirQueryConfigurationRequestApiModel
        {
            FacilityId = _facilityId,
            FhirServerBaseUrl = _b.AutomationCfg.InternalFhirServerBase,
            MaxConcurrentRequests = _b.AutomationCfg.FhirQuery.MaxConcurrentRequests,
            MaxRetries = 3
        };

        await RunAsync(results, "DataAcq.CreateFhirQueryConfiguration", async () =>
        {
            await _b.DataAcquisitionClient.CreateFhirQueryConfigurationAsync(queryConfigRequest);
            _queryConfigCreated = true;
        });

        await RunAsync(results, "DataAcq.GetFhirQueryConfiguration",
            () => _b.DataAcquisitionClient.GetFhirQueryConfigurationAsync(_facilityId));

        var dischargePlanBody = BuildQueryPlanRequest(_facilityId, measureId, "Discharge");
        await RunAsync(results, "DataAcq.CreateQueryPlan.Discharge", async () =>
        {
            await _b.DataAcquisitionClient.CreateQueryPlanAsync(_facilityId, dischargePlanBody);
            _dischargePlanCreated = true;
        });

        var monthlyPlanBody = BuildQueryPlanRequest(_facilityId, measureId, "Monthly");
        await RunAsync(results, "DataAcq.CreateQueryPlan.Monthly", async () =>
        {
            await _b.DataAcquisitionClient.CreateQueryPlanAsync(_facilityId, monthlyPlanBody);
            _monthlyPlanCreated = true;
        });

        await RunAsync(results, "DataAcq.GetQueryPlan.Discharge",
            () => _b.DataAcquisitionClient.GetQueryPlanAsync(_facilityId, "Discharge"));

        await RunAsync(results, "DataAcq.GetQueryPlan.Monthly",
            () => _b.DataAcquisitionClient.GetQueryPlanAsync(_facilityId, "Monthly"));

        var normalizationBody = new CreateNormalizationOperationRequestApiModel
        {
            ResourceTypes = ["Location"],
            FacilityId = _facilityId,
            Operation = new CreateNormalizationOperationDetailsApiModel
            {
                OperationType = "CopyProperty",
                Name = "Copy Location Identifier to Type",
                Description = "Api Stability Test Operation",
                SourceFhirPath = "identifier.value",
                TargetFhirPath = "type[0].coding.code"
            },
            Description = "Copy Location Identifier to Code",
            VendorIds = []
        };

        await RunAsync(results, "Normalization.CreateOperation", async () =>
        {
            await _b.NormalizationClient.CreateOperationAsync(normalizationBody);
            _normalizationCreated = true;
        });

        await RunAsync(results, "Normalization.SearchFacilityOperations",
            () => _b.NormalizationClient.SearchFacilityOperationsAsync(_facilityId));

        await RunAsync(results, "Normalization.GetOperationSequences",
            () => _b.NormalizationClient.GetOperationSequencesAsync(_facilityId));

        var censusConfig = new CensusConfigApiModel
        {
            FacilityId = _facilityId,
            ScheduledTrigger = "0 0/5 * * * ?",
            Enabled = true
        };

        await RunAsync(results, "Census.CreateConfig", async () =>
        {
            await _b.CensusClient.CreateCensusConfigAsync(censusConfig);
            _censusConfigCreated = true;
        });

        await RunAsync(results, "Census.GetConfig",
            () => _b.CensusClient.GetCensusConfigAsync(_facilityId));

        await RunAsync(results, "Census.UpdateConfig",
            () => _b.CensusClient.UpdateCensusConfigAsync(_facilityId, censusConfig));

        var rangeStart = DateTime.SpecifyKind(DateTime.Parse(_config.StartDate), DateTimeKind.Utc);
        var rangeEnd = DateTime.SpecifyKind(DateTime.Parse(_config.EndDate), DateTimeKind.Utc);

        await RunAsync(results, "Census.GetAdmittedPatients",
            () => _b.CensusClient.GetAdmittedPatientsAsync(_facilityId, rangeStart, rangeEnd));

        await RunAsync(results, "Census.GetCurrentPatientEncounters",
            () => _b.CensusClient.GetCurrentPatientEncountersAsync(_facilityId));

        await RunAsync(results, "Census.GetHistoricalPatientEncounters",
            () => _b.CensusClient.GetHistoricalPatientEncountersAsync(_facilityId, DateTime.UtcNow));

        await RunAsync(results, "Census.RebuildPatientEncounters",
            () => _b.CensusClient.RebuildPatientEncountersAsync(_facilityId));

        await RunAsync(results, "Census.GetPatientEvents",
            () => _b.CensusClient.GetPatientEventsAsync(_facilityId));

        await RunAsync(results, "Census.DeletePatientEvent",
            () => _b.CensusClient.DeletePatientEventAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Census.DeletePatientEventsByCorrelation",
            () => _b.CensusClient.DeletePatientEventsByCorrelationAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.GetSchedule",
            () => _b.ReportClient.GetScheduleAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.SearchSchedules",
            () => _b.ReportClient.SearchSchedulesAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.GetEntriesBySchedule",
            () => _b.ReportClient.GetEntriesByScheduleAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.SearchResources",
            () => _b.ReportClient.SearchResourcesAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.GetPopulationsBySchedule",
            () => _b.ReportClient.GetPopulationsByScheduleAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.SearchAcquisitionLogs",
            () => _b.DataAcquisitionClient.SearchAcquisitionLogsAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.SearchDetailedAcquisitionLogs",
            () => _b.DataAcquisitionClient.SearchDetailedAcquisitionLogsAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.GetReportStatusCounts",
            () => _b.DataAcquisitionClient.GetReportStatusCountsAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.GetAcquiredResourceIdsForReport",
            () => _b.DataAcquisitionClient.GetAcquiredResourceIdsForReportAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "Validation.GetValidationResults",
            () => _b.SdkValidationClient.GetValidationResultsAsync(_facilityId, Guid.NewGuid().ToString()));

        if (_monthlyPlanCreated)
        {
            await RunAsync(results, "DataAcq.DeleteQueryPlan.Monthly",
                () => _b.DataAcquisitionClient.DeleteQueryPlanAsync(_facilityId, "Monthly"));
            _monthlyPlanCreated = false;
        }

        if (_dischargePlanCreated)
        {
            await RunAsync(results, "DataAcq.DeleteQueryPlan.Discharge",
                () => _b.DataAcquisitionClient.DeleteQueryPlanAsync(_facilityId, "Discharge"));
            _dischargePlanCreated = false;
        }

        if (_queryConfigCreated)
        {
            await RunAsync(results, "DataAcq.DeleteFhirQueryConfiguration",
                () => _b.DataAcquisitionClient.DeleteFhirQueryConfigurationAsync(_facilityId));
            _queryConfigCreated = false;
        }

        if (_normalizationCreated)
        {
            await RunAsync(results, "Normalization.DeleteFacilityOperations",
                () => _b.NormalizationClient.DeleteFacilityOperationsAsync(_facilityId));
            _normalizationCreated = false;
        }

        if (_censusConfigCreated)
        {
            await RunAsync(results, "Census.DeleteConfig",
                () => _b.CensusClient.DeleteCensusConfigAsync(_facilityId));
            _censusConfigCreated = false;
        }

        if (_facilityCreated)
        {
            await RunAsync(results, "Facility.Delete",
                () => _b.FacilityClient.DeleteAsync(_facilityId));
            _facilityCreated = false;
        }

        Assert.False(results.Errors.Any(), results.ToAssertionMessage());
    }

    private static CreateQueryPlanRequestApiModel BuildQueryPlanRequest(string facilityId, string measureId, string type)
    {
        var jBody = QueryPlanBuilder.BuildQueryPlan(facilityId, measureId, "Epic", type);
        return new CreateQueryPlanRequestApiModel
        {
            PlanName = jBody.Value<string>("PlanName"),
            FacilityId = jBody.Value<string>("FacilityId") ?? facilityId,
            EHRDescription = jBody.Value<string>("EHRDescription") ?? "Epic",
            LookBack = jBody.Value<string>("LookBack") ?? "P0D",
            Type = jBody.Value<string>("Type") ?? type,
            InitialQueries = jBody["InitialQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(),
            SupplementalQueries = jBody["SupplementalQueries"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>()
        };
    }

    private async Task RunAsync(ApiRunResults results, string name, Func<Task> action)
    {
        try
        {
            await action();
            results.AddSuccess(name);
        }
        catch (Exception ex)
        {
            results.AddError(name, ex.Message);
        }
    }

    private async Task RunWithRetryAsync(ApiRunResults results, string name, Func<Task> action,
        TimeSpan timeout, TimeSpan retryDelay)
    {
        var started = DateTime.UtcNow;
        Exception? last = null;

        while (DateTime.UtcNow - started < timeout)
        {
            try
            {
                await action();
                results.AddSuccess(name);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(retryDelay);
            }
        }

        results.AddError(name, last?.Message ?? "timed out");
    }

    private static Task Try(Func<Task> action) =>
        action().ContinueWith(_ => { }, TaskContinuationOptions.None);

    private sealed class ApiRunResults(IAutomationOutput output)
    {
        private readonly List<string> _errors = [];
        public IReadOnlyList<string> Errors => _errors;

        public void AddSuccess(string apiCall, string? detail = null) =>
            output.WriteLine($"[API][PASS] {apiCall}{(detail != null ? $" | {detail}" : "")}");

        public void AddError(string apiCall, string detail)
        {
            _errors.Add($"{apiCall} | {detail}");
            output.WriteLine($"[API][FAIL] {apiCall} | {detail}");
        }

        public string ToAssertionMessage() =>
            "API stability failures:\n - " + string.Join("\n - ", _errors);
    }
}
