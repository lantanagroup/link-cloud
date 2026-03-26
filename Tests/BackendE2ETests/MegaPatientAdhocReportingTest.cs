using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Automation.Validation;
using RestSharp;
using System.Reflection;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Stress/volume test that generates 5 synthetic patients, each with over 10,000
/// FHIR resources, and runs them through the full ad-hoc reporting pipeline.
/// </summary>
public sealed class MegaPatientAdhocReportingTest : IAsyncLifetime
{
    private const string FacilityId = "MegaPatientTestFacility";

    private static readonly AutomationConfig AutomationCfg = TestConfig.BuildAutomationConfig();
    private static readonly TestScenarioConfig Config = TestConfig.MegaPatientTestConfig;
    private static readonly FhirDataLoader FhirDataLoader = new(AutomationCfg.ExternalFhirServerBase, AutomationCfg);
    private static readonly DatabaseConnectionFactory DbFactory = new(AutomationCfg.Database);

    private readonly DualOutputHelper _output;
    private readonly RestClient _adminBffClient = AdminBffClientFactory.Create(AutomationCfg);
    private readonly LokiScraper _lokiScraper;

    private FacilityApiClient FacilityApi => new(_adminBffClient, _output);
    private NormalizationApiClient NormalizationApi => new(_adminBffClient, _output);
    private QueryConfigApiClient QueryConfigApi => new(_adminBffClient, _output, AutomationCfg);
    private ReportApiClient ReportApi => new(_adminBffClient, _output, _lokiScraper, AutomationCfg, Config);
    private ValidationApiClient ValidationApi => new(_adminBffClient, _output, _lokiScraper);

    public MegaPatientAdhocReportingTest()
    {
        _output = new DualOutputHelper();
        _lokiScraper = new LokiScraper(_output, AutomationCfg);
    }

    public async Task InitializeAsync()
    {
        // Generate synthetic patients and load them onto the FHIR server
        var (patientIds, bundles) = FhirBundleGenerator.Generate(_output);

        // If config has no patient IDs set (the default), use generated ones
        if (Config.PatientIds.Count == 0)
        {
            Config.PatientIds = patientIds;
        }

        _output.WriteLine($"Patient IDs for test: [{string.Join(", ", Config.PatientIds)}]");

        // Wait for FHIR server before uploading large volume of bundles
        await FhirDataLoader.WaitForServerAsync(_output);

        // Load the generated bundles
        await FhirDataLoader.LoadTransactionBundlesFromJsonAsync(_output, bundles);

        // Also load any standard embedded bundles (shared test infrastructure like Lists)
        await FhirDataLoader.LoadEmbeddedTransactionBundles(_output, Assembly.GetExecutingAssembly());

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

        if (Config.RemoveReport)
        {
            // TODO: Delete report
        }
    }

    [Fact]
    [Trait("Category", "MegaPatientTest")]
    public async Task ExecuteMegaPatientTest()
    {
        // Step 1: Load measure definition into measureeval and validation
        var measureLoader = new MeasureLoader(_adminBffClient, _output, Config, Assembly.GetExecutingAssembly());
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
        await using var diagnostics = new BackgroundDiagnosticsMonitor(_output, _lokiScraper, AutomationCfg, Config.PatientIds.Count);
        await diagnostics.StartAsync(FacilityId, reportId);

        var reportSubmitted = await ReportApi.CheckSubmissionStatusAsync(reportId, diagnostics);

        await diagnostics.StopAsync();

        // Scrape measureeval and validation service logs for the full test duration
        _output.WriteLine("");
        _output.WriteLine("[DIAG] Scraping MeasureEval service logs...");
        await _lokiScraper.ScrapeServiceHistoryAsync(LokiScraper.Components.MeasureEval, Config.LokiScrapeWindow, "DIAG MEASUREEVAL");
        _output.WriteLine("[DIAG] Scraping Validation service logs...");
        await _lokiScraper.ScrapeServiceHistoryAsync(LokiScraper.Components.Validation, Config.LokiScrapeWindow, "DIAG VALIDATION");
        _output.WriteLine("[DIAG] Scraping Normalization service logs...");
        await _lokiScraper.ScrapeServiceHistoryAsync(LokiScraper.Components.Normalization, Config.LokiScrapeWindow, "DIAG NORMALIZATION");
        _output.WriteLine("[DIAG] Scraping Report service logs...");
        await _lokiScraper.ScrapeServiceHistoryAsync(LokiScraper.Components.Report, Config.LokiScrapeWindow, "DIAG REPORT");

        // Always write a snapshot before any assertions can kill the test.
        var pipelineSnapshot = new PipelineSnapshot(DbFactory);
        await pipelineSnapshot.WriteFullSnapshotAsync(_output, FacilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        // Step 8: Download and validate the report contents
        var downloadedResources = await ReportApi.DownloadReportAsync(FacilityId, reportId);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        foreach (var patientId in Config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected report to include patient-{patientId}.ndjson but it was not");
        }

        _output.WriteLine("Done generating and validating report.");

        // Step 9-10: Strict database validation
        var reportDbValidator = new ReportDatabaseValidator(_output, DbFactory);
        await reportDbValidator.ValidateAllAsync(
            FacilityId,
            reportId,
            measureId,
            Config.PatientIds);

        var dataAcqValidator = new DataAcquisitionDatabaseValidator(_output, DbFactory);
        await dataAcqValidator.ValidateAllAsync(
            FacilityId,
            reportId,
            measureId,
            Config.PatientIds);

        var normalizationValidator = new NormalizationDatabaseValidator(_output, DbFactory);
        await normalizationValidator.ValidateAllAsync(FacilityId);

        var tenantValidator = new TenantDatabaseValidator(_output, DbFactory);
        await tenantValidator.ValidateAllAsync(FacilityId, measureId);

        // Step 11: Validation results (API-based)
        var validationResultsValidator = new ValidationResultsValidator(_adminBffClient, _output);
        await validationResultsValidator.ValidateAllAsync(
            FacilityId,
            reportId,
            Config.PatientIds);
    }
}
