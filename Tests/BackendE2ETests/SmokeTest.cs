using System.Net;
using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// End-to-end smoke test that runs a single generated patient through the full
/// ad-hoc reporting pipeline.
/// </summary>
public sealed class SmokeTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const string FacilityId = "SmokeTestFacility";
    private const int GenerationSeed = 20260326;

    private static readonly TestScenarioConfig Config = TestConfig.AdhocReportingSmokeTestConfig;

    private readonly TestServices _b;
    private List<(string Name, string Json)> _generatedBundles = [];

    private AutomationConfig AutomationCfg => _b.AutomationCfg;
    private DualOutputHelper _output => _b.Output;
    private FhirDataLoader FhirDataLoader => _b.FhirDataLoader;

    private FacilityApiClient FacilityApi => _b.CreateFacilityApi();
    private NormalizationApiClient NormalizationApi => _b.CreateNormalizationApi();
    private QueryConfigApiClient QueryConfigApi => _b.CreateQueryConfigApi();
    private ReportApiClient ReportApi => _b.CreateReportApi(Config);
    private ValidationApiClient ValidationApi => _b.CreateValidationApi();

    public SmokeTest(BackendE2ETestFixture fixture)
    {
        _b = fixture.GetTestServices();
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var (patientIds, bundles) = FhirBundleGenerator.Generate(_output, 1, 1000, "SmokePatient", GenerationSeed);
        _generatedBundles = bundles;

        if (Config.PatientIds.Count == 0)
        {
            Config.PatientIds = patientIds;
        }

        await GeneratedFhirDataSnapshotWriter.WriteIfChangedAsync(
            _output,
            nameof(SmokeTest),
            GenerationSeed,
            Config.PatientIds,
            bundles);

        _output.WriteLine($"Patient IDs for test: [{string.Join(", ", Config.PatientIds)}]");

        await FhirDataLoader.WaitForServerAsync(_output);
        await FhirDataLoader.LoadTransactionBundlesFromJsonAsync(_output, bundles);

        await ValidationApi.InitializeArtifactsAsync();
        await ValidationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        _output.WriteLine("Cleaning up...\n");

        if (Config.RemoveFacilityConfig)
        {
            await FacilityApi.DeleteAsync(FacilityId);
        }

        if (AutomationCfg.CleanupTestData)
        {
            FhirDataLoader.ExpungeEverything(_output);
        }
    }

    [Fact]
    [Trait("Category", "SmokeTest")]
    public async Task ExecuteSmokeTest()
    {
        // Step 1: Load measure definition into MeasureEval and Validation.
        var measureLoader = new MeasureLoader(_b.MeasureEvalClient, _b.SdkValidationClient, _output, Config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        // Step 2: Create facility.
        await FacilityApi.CreateAsync(FacilityId, measureId);

        // Step 3: Create normalization config.
        await NormalizationApi.CreateConfigAsync(FacilityId);

        // Step 4: Create query plans (Discharge + Monthly).
        await QueryConfigApi.CreateQueryPlanAsync(FacilityId, measureId, "Epic");

        // Step 5: Create FHIR query config.
        await QueryConfigApi.CreateQueryConfigAsync(FacilityId);

        // Step 6: Validate core service endpoints before running heavy end-to-end report flow.
        await ValidateSdkEndpointsPrePipelineAsync(FacilityId, measureId);

        // Step 7: Generate the ad-hoc report.
        var reportId = await ReportApi.GenerateReportAsync(FacilityId, measureId);

        // Step 8: Start background diagnostics and poll until submitted.
        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            _output,
            _b.LokiScraper,
            AutomationCfg,
            Config.PatientIds.Count,
            forwardInternalLogsToOutput: false);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, _output);

        await diagnostics.StartAsync(FacilityId, reportId);

        var reportSubmitted = await ReportApi.CheckSubmissionStatusAsync(reportId, diagnostics);
        await diagnostics.StopAsync();
        await watcher.StopAsync();

        // Always capture a non-asserting snapshot before assertions.
        var pipelineSnapshot = _b.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(_output, FacilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        await ValidateSdkEndpointsAsync(FacilityId, reportId, measureId);

        // Step 9: Download and validate report artifacts.
        var downloadedResources = await ReportApi.DownloadReportAsync(FacilityId, reportId);
        var internalAbsResources = await ReportApi.DownloadReportAsync(FacilityId, reportId, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        foreach (var patientId in Config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected report to include patient-{patientId}.ndjson but it was not");
        }

        _output.WriteLine("Done generating and validating report.");

        await _b.CreateReportAbsManifestValidator().ValidateAllAsync(
            internalAbsResources,
            Config.PatientIds,
            measureId,
            Config.StartDate,
            Config.EndDate,
            FacilityId,
            reportId,
            null);

        await ValidationBaselineManager.ValidateOrCreateAsync(
            _output,
            _b.DataReader,
            nameof(SmokeTest),
            FacilityId,
            reportId,
            measureId,
            Config.StartDate,
            Config.EndDate,
            Config.PatientIds,
            _generatedBundles,
            internalAbsResources);

        // Step 9-10: Strict database validation.
        await _b.CreateReportValidator().ValidateAllAsync(FacilityId, reportId, measureId, Config.PatientIds);
        await _b.CreateDataAcqValidator().ValidateAllAsync(FacilityId, reportId, measureId, Config.PatientIds);
        await _b.CreateNormalizationValidator().ValidateAllAsync(FacilityId);
        await _b.CreateTenantValidator().ValidateAllAsync(FacilityId, measureId);
        await _b.CreateValidationResultsValidator().ValidateAllAsync(FacilityId, reportId, Config.PatientIds, Config.LokiScrapeWindow);
    }

    private async Task ValidateSdkEndpointsPrePipelineAsync(string facilityId, string measureId)
    {
        _output.WriteLine("Running pre-pipeline LinkSdk endpoint conformance checks...");

        var (measureStatus, _) = await _b.MeasureEvalClient.GetMeasureDefinitionAsync(measureId);
        Assert.Equal(HttpStatusCode.OK, measureStatus);

        Assert.Equal(HttpStatusCode.OK, await _b.FacilityClient.GetAsync(facilityId));
        var (facilityStatus, facilityDetails) = await _b.FacilityClient.GetDetailsAsync(facilityId);
        Assert.Equal(HttpStatusCode.OK, facilityStatus);
        Assert.NotNull(facilityDetails);

        var (opsStatus, opsResponse) = await _b.NormalizationClient.SearchFacilityOperationsAsync(facilityId);
        Assert.Equal(HttpStatusCode.OK, opsStatus);
        Assert.NotNull(opsResponse);

        var (seqStatus, _) = await _b.NormalizationClient.GetOperationSequencesAsync(facilityId);
        Assert.Equal(HttpStatusCode.OK, seqStatus);

        Assert.Equal(HttpStatusCode.OK, await _b.DataAcquisitionClient.GetFhirQueryConfigurationAsync(facilityId));
        Assert.Equal(HttpStatusCode.OK, await _b.DataAcquisitionClient.GetQueryPlanAsync(facilityId, "Discharge"));
        Assert.Equal(HttpStatusCode.OK, await _b.DataAcquisitionClient.GetQueryPlanAsync(facilityId, "Monthly"));

        var (censusConfigStatus, _) = await _b.CensusClient.GetCensusConfigAsync(facilityId);
        Assert.True(censusConfigStatus is HttpStatusCode.OK or HttpStatusCode.NotFound);

        _output.WriteLine("Pre-pipeline LinkSdk endpoint conformance checks completed.");
    }

    private async Task ValidateSdkEndpointsAsync(string facilityId, string reportId, string measureId)
    {
        _output.WriteLine("Running LinkSdk endpoint conformance checks...");

        var (measureStatus, _) = await _b.MeasureEvalClient.GetMeasureDefinitionAsync(measureId);
        Assert.Equal(HttpStatusCode.OK, measureStatus);

        Assert.Equal(HttpStatusCode.OK, await _b.FacilityClient.GetAsync(facilityId));
        var (facilityStatus, facilityDetails) = await _b.FacilityClient.GetDetailsAsync(facilityId);
        Assert.Equal(HttpStatusCode.OK, facilityStatus);
        Assert.NotNull(facilityDetails);

        var (opsStatus, opsResponse) = await _b.NormalizationClient.SearchFacilityOperationsAsync(facilityId);
        Assert.Equal(HttpStatusCode.OK, opsStatus);
        Assert.NotNull(opsResponse);

        var (seqStatus, _) = await _b.NormalizationClient.GetOperationSequencesAsync(facilityId);
        Assert.Equal(HttpStatusCode.OK, seqStatus);

        Assert.Equal(HttpStatusCode.OK, await _b.DataAcquisitionClient.GetFhirQueryConfigurationAsync(facilityId));
        Assert.Equal(HttpStatusCode.OK, await _b.DataAcquisitionClient.GetQueryPlanAsync(facilityId, "Discharge"));
        Assert.Equal(HttpStatusCode.OK, await _b.DataAcquisitionClient.GetQueryPlanAsync(facilityId, "Monthly"));

        var (scheduleStatus, schedule) = await _b.ReportClient.GetScheduleAsync(reportId);
        Assert.Equal(HttpStatusCode.OK, scheduleStatus);
        Assert.NotNull(schedule);

        var (searchStatus, search) = await _b.ReportClient.SearchSchedulesAsync(reportId);
        Assert.Equal(HttpStatusCode.OK, searchStatus);
        Assert.NotNull(search);

        var (entriesStatus, entries) = await _b.ReportClient.GetEntriesByScheduleAsync(reportId);
        Assert.Equal(HttpStatusCode.OK, entriesStatus);
        Assert.NotNull(entries);

        var (resourcesStatus, resources) = await _b.ReportClient.SearchResourcesAsync(facilityId, reportId);
        Assert.Equal(HttpStatusCode.OK, resourcesStatus);
        Assert.NotNull(resources);

        var (popStatus, populations) = await _b.ReportClient.GetPopulationsByScheduleAsync(reportId);
        Assert.Equal(HttpStatusCode.OK, popStatus);
        Assert.NotNull(populations);

        var (downloadStatus, bytes, _, _) = await _b.ReportClient.DownloadSubmissionAsync(facilityId, reportId, external: true);
        Assert.Equal(HttpStatusCode.OK, downloadStatus);
        Assert.NotNull(bytes);

        var validationStatus = await _b.SdkValidationClient.GetValidationResultsAsync(facilityId, reportId);
        Assert.True(validationStatus is HttpStatusCode.OK or HttpStatusCode.NotFound);

        var (censusConfigStatus, _) = await _b.CensusClient.GetCensusConfigAsync(facilityId);
        Assert.True(censusConfigStatus is HttpStatusCode.OK or HttpStatusCode.NotFound);

        _output.WriteLine("LinkSdk endpoint conformance checks completed.");
    }
}
