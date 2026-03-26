using LantanaGroup.Link.Tests.E2ETests.Helpers;
using LantanaGroup.Link.Tests.E2ETests.Services;
using LantanaGroup.Link.Tests.E2ETests.Validation;
using RestSharp;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

public sealed class SmokeTest : IAsyncLifetime
{
    private const string FacilityId = "SmokeTestFacility";

    private static readonly FhirDataLoader FhirDataLoader = new(TestConfig.ExternalFhirServerBase);
    private static readonly TestConfig.SmokeTestConfig Config = TestConfig.AdhocReportingSmokeTestConfig;

    private readonly DualOutputHelper _output;
    private readonly RestClient _adminBffClient = AdminBffClientFactory.Create();
    private readonly LokiScraper _lokiScraper;

    private FacilityApiClient FacilityApi => new(_adminBffClient, _output);
    private NormalizationApiClient NormalizationApi => new(_adminBffClient, _output);
    private QueryConfigApiClient QueryConfigApi => new(_adminBffClient, _output);
    private ReportApiClient ReportApi => new(_adminBffClient, _output, _lokiScraper, Config);
    private ValidationApiClient ValidationApi => new(_adminBffClient, _output, _lokiScraper);

    public SmokeTest()
    {
        _output = new DualOutputHelper();
        _lokiScraper = new LokiScraper(_output);
    }

    public async Task InitializeAsync()
    {
        // Wait for FHIR server before uploading bundles
        await FhirDataLoader.WaitForServerAsync(_output);

        // Load FHIR test data onto the external FHIR server
        await FhirDataLoader.LoadEmbeddedTransactionBundles(_output);

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

        if (TestConfig.CleanupSmokeTestData)
        {
            FhirDataLoader.ExpungeEverything(_output);
        }

        if (Config.RemoveReport)
        {
            // TODO: Delete report
        }
    }

    [Fact]
    [Trait("Category", "SmokeTest")]
    public async Task ExecuteSmokeTest()
    {
        // Step 1: Load measure definition into measureeval and validation
        var measureLoader = new MeasureLoader(_adminBffClient, _output, Config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

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
        await using var diagnostics = new BackgroundDiagnosticsMonitor(_output, _lokiScraper, Config.PatientIds.Count);
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
        // This guarantees the full pipeline state is in the output for debugging.
        await PipelineSnapshot.WriteFullSnapshotAsync(_output, FacilityId, reportId);

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

        // Step 9-10: Strict database validation (snapshot is already captured above
        // even if an assertion fails here)
        var reportDbValidator = new ReportDatabaseValidator(_output);
        await reportDbValidator.ValidateAllAsync(
            FacilityId,
            reportId,
            measureId,
            Config.PatientIds);

        var dataAcqValidator = new DataAcquisitionDatabaseValidator(_output);
        await dataAcqValidator.ValidateAllAsync(
            FacilityId,
            reportId,
            measureId,
            Config.PatientIds);

        var normalizationValidator = new NormalizationDatabaseValidator(_output);
        await normalizationValidator.ValidateAllAsync(FacilityId);

        var tenantValidator = new TenantDatabaseValidator(_output);
        await tenantValidator.ValidateAllAsync(FacilityId, measureId);

        // Step 11: Validation results (API-based) -- explains why FailedValidation occurs
        var validationResultsValidator = new ValidationResultsValidator(_adminBffClient, _output);
        await validationResultsValidator.ValidateAllAsync(
            FacilityId,
            reportId,
            Config.PatientIds);
    }
}
