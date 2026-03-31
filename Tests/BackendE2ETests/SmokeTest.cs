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

    private ReportApiHelper ReportApi => _b.CreateReportHelper();
    private ValidationApiHelper ValidationApi => _b.CreateValidationHelper();

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
            await SdkSetupHelper.CleanupFacilityAsync(_b, FacilityId);
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
        await SdkSetupHelper.EnsureFacilityAsync(_b, FacilityId, measureId);

        // Step 3: Create normalization config.
        await SdkSetupHelper.EnsureNormalizationConfigAsync(_b, FacilityId);

        // Step 4: Create query plans (Discharge + Monthly).
        await SdkSetupHelper.EnsureQueryPlansAsync(_b, FacilityId, measureId, "Epic");

        // Step 5: Create FHIR query config.
        await SdkSetupHelper.EnsureQueryConfigAsync(_b, FacilityId);

        // Step 6: Generate the ad-hoc report.
        var reportId = await ReportApi.GenerateReportAsync(FacilityId, measureId, Config);

        // Step 7: Start background diagnostics and poll until submitted.
        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            _output,
            _b.LokiScraper,
            AutomationCfg,
            Config.PatientIds.Count,
            forwardInternalLogsToOutput: false,
            pipelineReader: _b.DataReader);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, _output);

        await diagnostics.StartAsync(FacilityId, reportId);

        var reportSubmitted = await ReportApi.CheckSubmissionStatusAsync(reportId, Config, diagnostics);
        await diagnostics.StopAsync();
        await watcher.StopAsync();

        // Always capture a non-asserting snapshot before assertions.
        var pipelineSnapshot = _b.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(_output, FacilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        // Step 8: Download and validate report artifacts.
        var downloadedResources = await ReportApi.DownloadReportAsync(FacilityId, reportId, Config);
        var internalAbsResources = await ReportApi.DownloadReportAsync(FacilityId, reportId, Config, external: false);

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
            GeneratedFhirDataSnapshotWriter.GetSnapshotDirectory(nameof(SmokeTest)));

        await ValidationBaselineManager.ValidateOrCreateAsync(
            _output,
            _b.DataReader,
            nameof(SmokeTest),
            FacilityId,
            reportId,
            measureId,
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
}
