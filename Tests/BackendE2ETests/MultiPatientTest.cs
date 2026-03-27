using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Volume test that generates 1000 synthetic patients, each with ~100 FHIR resources,
/// and runs them through the full ad-hoc reporting pipeline.
///
/// Configuration is driven by MULTI_PATIENT_TEST_* environment variables.
/// </summary>
public sealed class MultiPatientTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const string FacilityId = "MultiPatientTestFacility";
    private const int GenerationSeed = 20260328;

    private static readonly TestScenarioConfig Config = TestConfig.BuildScenarioConfig("MULTI_PATIENT_TEST");

    private readonly TestServices _tesetServices;

    private AutomationConfig AutomationCfg => _tesetServices.AutomationCfg;
    private DualOutputHelper _output => _tesetServices.Output;
    private FhirDataLoader FhirDataLoader => _tesetServices.FhirDataLoader;

    private FacilityApiClient FacilityApi => _tesetServices.CreateFacilityApi();
    private NormalizationApiClient NormalizationApi => _tesetServices.CreateNormalizationApi();
    private QueryConfigApiClient QueryConfigApi => _tesetServices.CreateQueryConfigApi();
    private ReportApiClient ReportApi => _tesetServices.CreateReportApi(Config);
    private ValidationApiClient ValidationApi => _tesetServices.CreateValidationApi();

    public MultiPatientTest(BackendE2ETestFixture fixture)
    {
        _tesetServices = fixture.GetTestServices();
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        // Generate 1000 synthetic patients, each with ~100 resources
        var (patientIds, bundles) = FhirBundleGenerator.Generate(_output, 1000, 100, "MultiPatient", GenerationSeed);

        // If config has no patient IDs set (the default), use generated ones
        if (Config.PatientIds.Count == 0)
        {
            Config.PatientIds = patientIds;
        }

        await GeneratedFhirDataSnapshotWriter.WriteIfChangedAsync(
            _output,
            nameof(MultiPatientTest),
            GenerationSeed,
            Config.PatientIds,
            bundles);

        _output.WriteLine($"Patient IDs for test: [{string.Join(", ", Config.PatientIds.Take(10))}...]");

        // Wait for FHIR server before uploading large volume of bundles
        await FhirDataLoader.WaitForServerAsync(_output);

        // Load the generated bundles
        await FhirDataLoader.LoadTransactionBundlesFromJsonAsync(_output, bundles);

        // Initialize validation artifacts and categories (with retry)
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
    [Trait("Category", "MultiPatientTest")]
    public async Task ExecuteMultiPatientTest()
    {
        // Step 1: Load measure definition into measureeval and validation
        var measureLoader = new MeasureLoader(_tesetServices.AdminBffClient, _output, Config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        _output.WriteLine($"MeasureId: {measureId}");
        _output.WriteLine($"Patients : {Config.PatientIds.Count}");
        _output.WriteLine($"Polling  : {Config.MaxRetryCount} retries x {Config.PollingIntervalSeconds}s = {Config.MaxPollingDuration.TotalSeconds:F0}s max");

        // Step 2: Create facility
        await FacilityApi.CreateAsync(FacilityId, measureId);

        // Step 3: Create normalization config
        await NormalizationApi.CreateConfigAsync(FacilityId);

        // Step 4: Create query plans (Discharge + Monthly)
        await QueryConfigApi.CreateQueryPlanAsync(FacilityId, measureId, "Epic");

        // Step 5: Create FHIR query config
        await QueryConfigApi.CreateQueryConfigAsync(FacilityId);

        // Step 6: Generate the ad-hoc report
        var reportId = await ReportApi.GenerateReportAsync(FacilityId, measureId);

        // Step 7: Start background diagnostics and poll until the report is submitted
        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            _output,
            _tesetServices.LokiScraper,
            AutomationCfg,
            Config.PatientIds.Count,
            forwardInternalLogsToOutput: false);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, _output);
        await diagnostics.StartAsync(FacilityId, reportId);

        var reportSubmitted = await ReportApi.CheckSubmissionStatusAsync(reportId, diagnostics);

        await diagnostics.StopAsync();
        await watcher.StopAsync();

        // Keep diagnostics output concise: rely on live background monitoring above,
        // and capture a single DB snapshot before assertions.
        // Always write a snapshot before any assertions can kill the test.
        var pipelineSnapshot = _tesetServices.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(_output, FacilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        // Step 8: Download and validate the report contents
        var downloadedResources = await ReportApi.DownloadReportAsync(FacilityId, reportId);
        var internalAbsResources = await ReportApi.DownloadReportAsync(FacilityId, reportId, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        foreach (var patientId in Config.PatientIds.Take(10)) // Only check first 10 for sanity
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected report to include patient-{patientId}.ndjson but it was not");
        }

        _output.WriteLine("Done generating and validating report.");

        await _tesetServices.CreateReportAbsManifestValidator().ValidateAllAsync(
            internalAbsResources,
            Config.PatientIds,
            measureId,
            Config.StartDate,
            Config.EndDate,
            FacilityId,
            reportId,
            GeneratedFhirDataSnapshotWriter.GetSnapshotDirectory(nameof(MultiPatientTest)));
    }
}

