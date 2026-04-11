using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Automation.Link.Validation;
using LantanaGroup.Link.Sdk.Clients;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// End-to-end smoke test that runs a single generated patient through the full
/// ad-hoc reporting pipeline.
/// </summary>
public sealed class SmokeTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260326;

    private static readonly TestScenarioConfig Config = TestConfig.AdhocReportingSmokeTestConfig;

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"SmokeTest-{Guid.NewGuid():N}";
    private List<(string Name, string Json)> _generatedBundles = [];
    private string? _reportId;

    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private ConsoleAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();
    private FhirDataLoader FhirDataLoader => _sp.GetRequiredService<FhirDataLoader>();

    public SmokeTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    public async Task InitializeAsync()
    {
        _sp.GetRequiredService<PipelineDataReader>().InvalidateCache();

        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var (patientIds, bundles) = FhirBundleGenerator.Generate(Output, 1, 1000, "SmokePatient", GenerationSeed);
        _generatedBundles = bundles;

        if (Config.PatientIds.Count == 0)
        {
            Config.PatientIds = patientIds;
        }

        Output.WriteLine($"Patient IDs for test: [{string.Join(", ", Config.PatientIds)}]");

        await FhirDataLoader.WaitForServerAsync(Output);
        await FhirDataLoader.LoadTransactionBundlesFromJsonAsync(Output, bundles);

        var validationApi = _sp.GetRequiredService<ValidationApiHelper>();
        await validationApi.InitializeArtifactsAsync();
        await validationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine("Cleaning up...\n");

        if (Config.RemoveFacilityConfig)
        {
            await FacilitySetupHelper.CleanupFacilityAsync(
                _sp.GetRequiredService<IFacilityServiceClient>(),
                _sp.GetRequiredService<INormalizationServiceClient>(),
                _sp.GetRequiredService<IDataAcquisitionServiceClient>(),
                _sp.GetRequiredService<IQueryDispatchServiceClient>(),
                Output,
                _facilityId);
        }

        if (AutomationCfg.CleanupTestData)
        {
            await FacilitySetupHelper.CleanupQueryDispatchConfigAsync(
                _sp.GetRequiredService<IQueryDispatchServiceClient>(),
                Output,
                _facilityId);

            if (!string.IsNullOrWhiteSpace(_reportId))
            {
                await FacilitySetupHelper.SoftDeleteRunDataAsync(
                    _sp.GetRequiredService<IReportServiceClient>(),
                    _sp.GetRequiredService<IDataAcquisitionServiceClient>(),
                    _sp.GetRequiredService<IQueryDispatchServiceClient>(),
                    Output,
                    _facilityId,
                    _reportId);
            }

            FhirDataLoader.ExpungeEverything(Output);
        }
    }

    [Fact]
    [Trait("Category", "SmokeTest")]
    public async Task ExecuteSmokeTest()
    {
        // Step 1: Load measure definition into MeasureEval and Validation.
        var measureLoader = new MeasureLoader(
            _sp.GetRequiredService<IMeasureEvalServiceClient>(),
            _sp.GetRequiredService<IValidationServiceClient>(),
            Output, Config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        // Step 2: Create facility.
        await FacilitySetupHelper.EnsureFacilityAsync(
            _sp.GetRequiredService<IFacilityServiceClient>(), Output, _facilityId, measureId);

        // Step 3: Create normalization config.
        await FacilitySetupHelper.EnsureNormalizationConfigAsync(
            _sp.GetRequiredService<INormalizationServiceClient>(), Output, _facilityId);

        // Step 4: Create query plans (Discharge + Monthly).
        await FacilitySetupHelper.EnsureQueryPlansAsync(
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(), Output, _facilityId, measureId, "Epic");

        // Step 5: Create FHIR query config.
        await FacilitySetupHelper.EnsureQueryConfigAsync(
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(), AutomationCfg, Output, _facilityId);

        await FacilitySetupHelper.EnsureQueryDispatchConfigAsync(
            _sp.GetRequiredService<IQueryDispatchServiceClient>(),
            Output,
            _facilityId);

        // Step 6: Generate the ad-hoc report.
        var reportApi = _sp.GetRequiredService<ReportApiHelper>();
        var reportId = await reportApi.GenerateReportAsync(_facilityId, measureId, Config);
        _reportId = reportId;

        // Step 7: Start background diagnostics and poll until submitted.
        var lokiScraper = _sp.GetRequiredService<LokiScraper>();
        var dataReader = _sp.GetRequiredService<PipelineDataReader>();

        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            Output, lokiScraper, AutomationCfg,
            Config.PatientIds.Count,
            forwardInternalLogsToOutput: false,
            pipelineReader: dataReader);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, Output);

        await diagnostics.StartAsync(_facilityId, reportId);

        var reportSubmitted = await reportApi.CheckSubmissionStatusAsync(reportId, Config, diagnostics);
        await diagnostics.StopAsync();
        await watcher.StopAsync();

        // Always capture a non-asserting snapshot before assertions.
        var pipelineSnapshot = _sp.GetRequiredService<PipelineSnapshot>();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        // Step 8: Download and validate report artifacts.
        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, reportId, Config);
        var internalAbsResources = await reportApi.DownloadReportAsync(_facilityId, reportId, Config, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        foreach (var patientId in Config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected report to include patient-{patientId}.ndjson but it was not");
        }

        Output.WriteLine("Done generating and validating report.");

        // Flush stale cache from diagnostics polling so validators read authoritative data.
        dataReader.InvalidateCache();

        await _sp.GetRequiredService<ReportAbsManifestValidator>().ValidateAllAsync(
            internalAbsResources,
            Config.PatientIds,
            measureId,
            Config.StartDate,
            Config.EndDate,
            _facilityId,
            reportId,
            _generatedBundles);

        await ValidationBaselineManager.ValidateOrCreateAsync(
            Output,
            dataReader,
            nameof(SmokeTest),
            _facilityId,
            reportId,
            measureId,
            Config.PatientIds,
            _generatedBundles,
            internalAbsResources);

        // Step 9-10: Strict database validation.
        await _sp.GetRequiredService<ReportDatabaseValidator>().ValidateAllAsync(_facilityId, reportId, measureId, Config.PatientIds);
        await _sp.GetRequiredService<DataAcquisitionDatabaseValidator>().ValidateAllAsync(_facilityId, reportId, measureId, Config.PatientIds);
        await _sp.GetRequiredService<NormalizationDatabaseValidator>().ValidateAllAsync(_facilityId);
        await _sp.GetRequiredService<TenantDatabaseValidator>().ValidateAllAsync(_facilityId, measureId);
        await _sp.GetRequiredService<ValidationResultsValidator>().ValidateAllAsync(_facilityId, reportId, Config.PatientIds, Config.LokiScrapeWindow);
    }
}
