using Flurl.Http;
using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.QueryDispatch;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class ApiStabilityTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private readonly TestScenarioConfig _config = TestConfig.BuildScenarioConfig(
        "API_STABILITY_TEST",
        defaultPatientIds: []);

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"ApiStabilityTest-{Guid.NewGuid():N}";

    private bool _facilityCreated;
    private bool _queryConfigCreated;
    private bool _dischargePlanCreated;
    private bool _monthlyPlanCreated;
    private bool _normalizationCreated;
    private bool _censusConfigCreated;
    private bool _queryDispatchConfigCreated;

    // Resolve clients once for readability
    private IAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();
    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private IFacilityServiceClient FacilityClient => _sp.GetRequiredService<IFacilityServiceClient>();
    private IDataAcquisitionServiceClient DataAcqClient => _sp.GetRequiredService<IDataAcquisitionServiceClient>();
    private INormalizationServiceClient NormalizationClient => _sp.GetRequiredService<INormalizationServiceClient>();
    private ICensusServiceClient CensusClient => _sp.GetRequiredService<ICensusServiceClient>();
    private IReportServiceClient ReportClient => _sp.GetRequiredService<IReportServiceClient>();
    private ISubmissionServiceClient SubmissionClient => _sp.GetRequiredService<ISubmissionServiceClient>();
    private IMeasureEvalServiceClient MeasureEvalClient => _sp.GetRequiredService<IMeasureEvalServiceClient>();
    private IValidationServiceClient ValidationClient => _sp.GetRequiredService<IValidationServiceClient>();
    private IQueryDispatchServiceClient QueryDispatchClient => _sp.GetRequiredService<IQueryDispatchServiceClient>();

    public ApiStabilityTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
        _config.CleanupServiceData = true;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!_config.CleanupServiceData)
            return;

        if (_monthlyPlanCreated)
            await Try(() => DataAcqClient.DeleteQueryPlanAsync(_facilityId, "Monthly"));
        if (_dischargePlanCreated)
            await Try(() => DataAcqClient.DeleteQueryPlanAsync(_facilityId, "Discharge"));
        if (_queryConfigCreated)
            await Try(() => DataAcqClient.DeleteFhirQueryConfigurationAsync(_facilityId));
        if (_normalizationCreated)
            await Try(() => NormalizationClient.DeleteFacilityOperationsAsync(_facilityId));
        if (_censusConfigCreated)
            await Try(() => CensusClient.DeleteCensusConfigAsync(_facilityId));
        if (_queryDispatchConfigCreated)
            await Try(() => QueryDispatchClient.DeleteQueryDispatchConfigurationAsync(_facilityId));
        if (_facilityCreated)
            await Try(() => FacilityClient.DeleteAsync(_facilityId));
    }

    [Fact]
    [Trait("Category", "ApiStabilityTest")]
    public async Task ExecuteApiStabilityTest()
    {
        var results = new ApiRunResults(Output);

        await RunWithRetryAsync(results, "Validation.InitializeArtifacts", async () =>
        {
            var hasArtifacts = await ValidationClient.HasArtifactsAsync();
            if (hasArtifacts)
            {
                Output.WriteLine("Validation artifacts already initialized. Skipping initialize call.");
                return;
            }

            await ValidationClient.InitializeArtifactsAsync();
        }, timeout: TimeSpan.FromMinutes(3), retryDelay: TimeSpan.FromSeconds(10));

        await RunWithRetryAsync(results, "Validation.InitializeCategories", async () =>
        {
            var hasCategories = await ValidationClient.HasCategoriesAsync();
            if (hasCategories)
            {
                Output.WriteLine("Validation categories already initialized. Skipping initialize call.");
                return;
            }

            await ValidationClient.InitializeCategoriesAsync();
        }, timeout: TimeSpan.FromMinutes(3), retryDelay: TimeSpan.FromSeconds(10));

        string? measureId = null;
        await RunAsync(results, "MeasureLoader.Load", async () =>
        {
            var measureLoader = new MeasureLoader(MeasureEvalClient, ValidationClient, Output, _config);
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
            () => MeasureEvalClient.GetMeasureDefinitionAsync(measureId));

        await RunAsync(results, "Validation.UpsertResourceArtifact", async () =>
        {
            var artifactId = $"OperationOutcome-{Guid.NewGuid():N}";
            var payload = $"{{\"resourceType\":\"OperationOutcome\",\"id\":\"{Guid.NewGuid():N}\",\"issue\":[{{\"severity\":\"information\",\"code\":\"informational\",\"diagnostics\":\"ApiStabilityTest artifact\"}}]}}";
            await ValidationClient.UpsertResourceArtifactAsync(artifactId, payload);
        });

        var facilityBody = new FacilityModel
        {
            FacilityId = _facilityId,
            FacilityName = _facilityId,
            TimeZone = "America/Chicago",
            Vendor = Vendor.Epic,
            ScheduledReports = new TenantScheduledReportConfig
            {
                Monthly = [measureId],
                Daily = [],
                Weekly = []
            }
        };

        await RunAsync(results, "Facility.Create", async () =>
        {
            await FacilityClient.CreateAsync(facilityBody);
            _facilityCreated = true;
        });

        await RunAsync(results, "Facility.Get",
            () => FacilityClient.GetAsync(_facilityId));

        await RunAsync(results, "Facility.CheckFacilityExists",
            () => FacilityClient.CheckFacilityExistsAsync(_facilityId));

        var queryDispatchConfig = new QueryDispatchConfigurationApiModel
        {
            FacilityId = _facilityId,
            DispatchSchedules =
            [
                new DispatchScheduleApiModel
                {
                    Event = "Discharge",
                    Duration = "PT0S"
                }
            ]
        };

        await RunAsync(results, "QueryDispatch.UpsertConfiguration", async () =>
        {
            await QueryDispatchClient.UpsertQueryDispatchConfigurationAsync(_facilityId, queryDispatchConfig);
            _queryDispatchConfigCreated = true;
        });

        var queryConfigRequest = new CreateFhirQueryConfigurationRequestApiModel
        {
            FacilityId = _facilityId,
            FhirServerBaseUrl = AutomationCfg.FacilityFhirServerBase,
            MaxConcurrentRequests = Math.Max(2, AutomationCfg.FhirQuery.MaxConcurrentRequests),
            MaxRetries = 3
        };

        await RunAsync(results, "DataAcq.CreateFhirQueryConfiguration", async () =>
        {
            await DataAcqClient.CreateFhirQueryConfigurationAsync(queryConfigRequest);
            _queryConfigCreated = true;
        });

        await RunAsync(results, "DataAcq.GetFhirQueryConfiguration",
            () => DataAcqClient.GetFhirQueryConfigurationAsync(_facilityId));

        var dischargePlanBody = BuildQueryPlanRequest(_facilityId, measureId, "Discharge");
        await RunAsync(results, "DataAcq.CreateQueryPlan.Discharge", async () =>
        {
            await DataAcqClient.CreateQueryPlanAsync(_facilityId, dischargePlanBody);
            _dischargePlanCreated = true;
        });

        var monthlyPlanBody = BuildQueryPlanRequest(_facilityId, measureId, "Monthly");
        await RunAsync(results, "DataAcq.CreateQueryPlan.Monthly", async () =>
        {
            await DataAcqClient.CreateQueryPlanAsync(_facilityId, monthlyPlanBody);
            _monthlyPlanCreated = true;
        });

        await RunAsync(results, "DataAcq.GetQueryPlan.Discharge",
            () => DataAcqClient.GetQueryPlanAsync(_facilityId, "Discharge"));

        await RunAsync(results, "DataAcq.GetQueryPlan.Monthly",
            () => DataAcqClient.GetQueryPlanAsync(_facilityId, "Monthly"));

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
            await NormalizationClient.CreateOperationAsync(normalizationBody);
            _normalizationCreated = true;
        });

        await RunAsync(results, "Normalization.SearchFacilityOperations",
            () => NormalizationClient.SearchFacilityOperationsAsync(_facilityId));

        await RunAsync(results, "Normalization.GetOperationSequences",
            () => NormalizationClient.GetOperationSequencesAsync(_facilityId));

        var censusConfig = new CensusConfigApiModel
        {
            FacilityId = _facilityId,
            ScheduledTrigger = "0 0/5 * * * ?",
            Enabled = true
        };

        await RunAsync(results, "Census.CreateConfig", async () =>
        {
            await CensusClient.CreateCensusConfigAsync(censusConfig);
            _censusConfigCreated = true;
        });

        await RunAsync(results, "Census.GetConfig",
            () => CensusClient.GetCensusConfigAsync(_facilityId));

        await RunAsync(results, "Census.UpdateConfig",
            () => CensusClient.UpdateCensusConfigAsync(_facilityId, censusConfig));

        var rangeStart = DateTime.SpecifyKind(DateTime.Parse(_config.StartDate), DateTimeKind.Utc);
        var rangeEnd = DateTime.SpecifyKind(DateTime.Parse(_config.EndDate), DateTimeKind.Utc);

        await RunAsync(results, "Census.GetAdmittedPatients",
            () => CensusClient.GetAdmittedPatientsAsync(_facilityId, rangeStart, rangeEnd));

        await RunAsync(results, "Census.GetCurrentPatientEncounters",
            () => CensusClient.GetCurrentPatientEncountersAsync(_facilityId));

        await RunAsync(results, "Census.GetHistoricalPatientEncounters",
            () => CensusClient.GetHistoricalPatientEncountersAsync(_facilityId, DateTime.UtcNow));

        await RunAsync(results, "Census.RebuildPatientEncounters",
            () => CensusClient.RebuildPatientEncountersAsync(_facilityId));

        await RunAsync(results, "Census.GetPatientEvents",
            () => CensusClient.GetPatientEventsAsync(_facilityId));

        await RunAsync(results, "Census.DeletePatientEvent",
            () => CensusClient.DeletePatientEventAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Census.DeletePatientEventsByCorrelation",
            () => CensusClient.DeletePatientEventsByCorrelationAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.GetSchedule",
            () => ReportClient.GetScheduleAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.SearchSchedules",
            () => ReportClient.SearchSchedulesAsync(Guid.NewGuid().ToString()));

        await RunExpecting404Async(results, "Report.SoftDeleteSchedule",
            () => ReportClient.SoftDeleteScheduleAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.GetEntriesBySchedule",
            () => ReportClient.GetEntriesByScheduleAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.SearchResources",
            () => ReportClient.SearchResourcesAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "Report.GetPopulationsBySchedule",
            () => ReportClient.GetPopulationsByScheduleAsync(Guid.NewGuid().ToString()));

        // Submission service – non-existent report returns 404; that proves the service is reachable
        await RunExpecting404Async(results, "Submission.DownloadSubmission",
            () => SubmissionClient.DownloadSubmissionAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.SearchAcquisitionLogs",
            () => DataAcqClient.SearchAcquisitionLogsAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.GetAcquisitionLogNotes",
            () => DataAcqClient.GetAcquisitionLogNotesAsync(1));

        await RunAsync(results, "DataAcq.GetReportStatistics",
            () => DataAcqClient.GetReportStatisticsAsync(Guid.NewGuid().ToString()));

        await RunExpectingStatusAsync(results, "DataAcq.ProcessAcquisitionLog", 404,
            () => DataAcqClient.ProcessAcquisitionLogAsync(long.MaxValue));

        await RunAsync(results, "DataAcq.ProcessAcquisitionLogsBulk",
            () => DataAcqClient.ProcessAcquisitionLogsBulkAsync([long.MaxValue]));

        await RunAsync(results, "DataAcq.CancelAcquisitionLogsBulk",
            () => DataAcqClient.CancelAcquisitionLogsBulkAsync([long.MaxValue], minAgeHours: 0));

        await RunAsync(results, "DataAcq.ProcessAcquisitionLogsByFilter",
            () => DataAcqClient.ProcessAcquisitionLogsByFilterAsync(new
            {
                FacilityId = _facilityId
            }));

        await RunAsync(results, "DataAcq.CancelAcquisitionLogsByFilter",
            () => DataAcqClient.CancelAcquisitionLogsByFilterAsync(new
            {
                FacilityId = _facilityId
            }, minAgeHours: 0));

        await RunAsync(results, "DataAcq.GetReportStatusCounts",
            () => DataAcqClient.GetReportStatusCountsAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.GetReportSummary",
            () => DataAcqClient.GetReportSummaryAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.GetAcquiredResourceIdsForReport",
            () => DataAcqClient.GetAcquiredResourceIdsForReportAsync(_facilityId, Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.GetReferenceResourcesForLog",
            () => DataAcqClient.GetReferenceResourcesForLogAsync(1));

        await RunAsync(results, "DataAcq.SoftDeleteLogsByFacility",
            () => DataAcqClient.SoftDeleteLogsByFacilityAsync(_facilityId));

        await RunAsync(results, "DataAcq.DeleteAcquisitionLog",
            () => DataAcqClient.DeleteAcquisitionLogAsync(long.MaxValue));

        await RunAsync(results, "DataAcq.SoftDeleteLogsByReportTrackingId",
            () => DataAcqClient.SoftDeleteLogsByReportTrackingIdAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.RestoreLogsByReportTrackingId",
            () => DataAcqClient.RestoreLogsByReportTrackingIdAsync(Guid.NewGuid().ToString()));

        await RunAsync(results, "DataAcq.RestoreLogsByFacility",
            () => DataAcqClient.RestoreLogsByFacilityAsync(_facilityId));

        await RunAsync(results, "Validation.GetValidationResults",
            () => ValidationClient.GetValidationResultsAsync(_facilityId, Guid.NewGuid().ToString()));

        if (_monthlyPlanCreated)
        {
            await RunAsync(results, "DataAcq.DeleteQueryPlan.Monthly",
                () => DataAcqClient.DeleteQueryPlanAsync(_facilityId, "Monthly"));
            _monthlyPlanCreated = false;
        }

        if (_dischargePlanCreated)
        {
            await RunAsync(results, "DataAcq.DeleteQueryPlan.Discharge",
                () => DataAcqClient.DeleteQueryPlanAsync(_facilityId, "Discharge"));
            _dischargePlanCreated = false;
        }

        if (_queryConfigCreated)
        {
            await RunAsync(results, "DataAcq.DeleteFhirQueryConfiguration",
                () => DataAcqClient.DeleteFhirQueryConfigurationAsync(_facilityId));
            _queryConfigCreated = false;
        }

        if (_normalizationCreated)
        {
            await RunAsync(results, "Normalization.DeleteFacilityOperations",
                () => NormalizationClient.DeleteFacilityOperationsAsync(_facilityId));
            _normalizationCreated = false;
        }

        if (_censusConfigCreated)
        {
            await RunAsync(results, "Census.DeleteConfig",
                () => CensusClient.DeleteCensusConfigAsync(_facilityId));
            _censusConfigCreated = false;
        }

        if (_queryDispatchConfigCreated)
        {
            await RunAsync(results, "QueryDispatch.DeleteConfiguration",
                () => QueryDispatchClient.DeleteQueryDispatchConfigurationAsync(_facilityId));
            _queryDispatchConfigCreated = false;
        }

        if (_facilityCreated)
        {
            await RunAsync(results, "Facility.Delete",
                () => FacilityClient.DeleteAsync(_facilityId));
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

    private async Task RunExpectingStatusAsync(ApiRunResults results, string name, int expectedStatusCode, Func<Task> action)
    {
        try
        {
            await action();
            results.AddError(name, $"Expected HTTP {expectedStatusCode} but call succeeded.");
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == expectedStatusCode)
        {
            results.AddSuccess(name, $"{expectedStatusCode} (expected)");
        }
        catch (Exception ex)
        {
            results.AddError(name, ex.Message);
        }
    }

    /// <summary>
    /// Runs an action where a 404 response is the expected healthy outcome
    /// (e.g., querying a non-existent resource to prove the service is reachable).
    /// </summary>
    private async Task RunExpecting404Async(ApiRunResults results, string name, Func<Task> action)
    {
        try
        {
            await action();
            results.AddSuccess(name);
        }
        catch (FlurlHttpException ex) when (ex.StatusCode == 404)
        {
            results.AddSuccess(name, "404 (expected for non-existent resource)");
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
