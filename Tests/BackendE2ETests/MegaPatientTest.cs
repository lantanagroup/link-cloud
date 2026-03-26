using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Stress/volume test that generates 5 synthetic patients, each with over 10,000
/// FHIR resources, and runs them through the full ad-hoc reporting pipeline.
/// </summary>
public sealed class MegaPatientTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const string FacilityId = "MegaPatientTestFacility";

    private static readonly TestScenarioConfig Config = TestConfig.MegaPatientTestConfig;

    private readonly TestServices _b;

    private AutomationConfig AutomationCfg => _b.AutomationCfg;
    private DualOutputHelper _output => _b.Output;
    private FhirDataLoader FhirDataLoader => _b.FhirDataLoader;

    private FacilityApiClient FacilityApi => _b.CreateFacilityApi();
    private NormalizationApiClient NormalizationApi => _b.CreateNormalizationApi();
    private QueryConfigApiClient QueryConfigApi => _b.CreateQueryConfigApi();
    private ReportApiClient ReportApi => _b.CreateReportApi(Config);
    private ValidationApiClient ValidationApi => _b.CreateValidationApi();

    public MegaPatientTest(BackendE2ETestFixture fixture)
    {
        _b = fixture.GetTestServices();
    }

    public async Task InitializeAsync()
    {
        var (patientIds, bundles) = FhirBundleGenerator.Generate(_output);

        if (Config.PatientIds.Count == 0)
        {
            Config.PatientIds = patientIds;
        }

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
    [Trait("Category", "MegaPatientTest")]
    public async Task ExecuteMegaPatientTest()
    {
        // Step 1: Load measure definition into MeasureEval and Validation.
        var measureLoader = new MeasureLoader(_b.AdminBffClient, _output, Config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        _output.WriteLine($"MeasureId: {measureId}");
        _output.WriteLine($"Patients : {Config.PatientIds.Count}");
        _output.WriteLine($"Polling  : {Config.MaxRetryCount} retries x {Config.PollingIntervalSeconds}s = {Config.MaxPollingDuration.TotalSeconds:F0}s max");

        // Step 2: Create facility.
        await FacilityApi.CreateAsync(FacilityId, measureId);

        // Step 3: Create normalization config.
        await NormalizationApi.CreateConfigAsync(FacilityId);

        // Step 4: Create query plans (Discharge + Monthly).
        await QueryConfigApi.CreateQueryPlanAsync(FacilityId, measureId, "Epic");

        // Step 5: Create FHIR query config.
        await QueryConfigApi.CreateQueryConfigAsync(FacilityId);

        // Step 6: Generate the ad-hoc report.
        var reportId = await ReportApi.GenerateReportAsync(FacilityId, measureId);

        // Step 7: Start background diagnostics and poll until submitted.
        await using var diagnostics = new BackgroundDiagnosticsMonitor(_output, _b.LokiScraper, AutomationCfg, Config.PatientIds.Count);
        await diagnostics.StartAsync(FacilityId, reportId);

        var reportSubmitted = await ReportApi.CheckSubmissionStatusAsync(reportId, diagnostics);
        await diagnostics.StopAsync();

        // Always capture a non-asserting snapshot before assertions.
        var pipelineSnapshot = _b.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(_output, FacilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        // Step 8: Download and validate report artifacts.
        var downloadedResources = await ReportApi.DownloadReportAsync(FacilityId, reportId);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        foreach (var patientId in Config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected report to include patient-{patientId}.ndjson but it was not");
        }

        _output.WriteLine("Done generating and validating report.");

        // Step 9-10: Strict database validation.
        await _b.CreateReportValidator().ValidateAllAsync(FacilityId, reportId, measureId, Config.PatientIds);
        await _b.CreateDataAcqValidator().ValidateAllAsync(FacilityId, reportId, measureId, Config.PatientIds);
        await _b.CreateNormalizationValidator().ValidateAllAsync(FacilityId);
        await _b.CreateTenantValidator().ValidateAllAsync(FacilityId, measureId);

        // Step 11: Validation results exception check (API + Validation service logs).
        await _b.CreateValidationResultsValidator().ValidateAllAsync(FacilityId, reportId, Config.PatientIds, Config.LokiScrapeWindow);
    }
}

