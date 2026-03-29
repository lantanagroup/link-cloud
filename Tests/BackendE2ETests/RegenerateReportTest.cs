using System.Globalization;
using System.Net;
using System.Text.Json;
using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using RestSharp;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// End-to-end test for the regenerate workflow via Tenant API -> GenerateReportRequested -> GenerateReportListener.
/// The source ad-hoc report intentionally uses an empty PatientIds list so the Report service resolves patients through Census.
/// </summary>
public sealed class RegenerateReportTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260401;

    private readonly TestScenarioConfig _config = TestConfig.BuildScenarioConfig(
        "REGENERATE_REPORT_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 3,
        defaultMaxRetryCount: 120,
        defaultLokiScrapeWindowMinutes: 10);

    private readonly TestServices _b;
    private readonly string _facilityId = $"RegenTest-{Guid.NewGuid():N}";
    private List<(string Name, string Json)> _generatedBundles = [];

    private AutomationConfig AutomationCfg => _b.AutomationCfg;
    private DualOutputHelper Output => _b.Output;
    private FhirDataLoader FhirDataLoader => _b.FhirDataLoader;

    private FacilityApiClient FacilityApi => _b.CreateFacilityApi();
    private NormalizationApiClient NormalizationApi => _b.CreateNormalizationApi();
    private QueryConfigApiClient QueryConfigApi => _b.CreateQueryConfigApi();
    private ValidationApiClient ValidationApi => _b.CreateValidationApi();

    public RegenerateReportTest(BackendE2ETestFixture fixture)
    {
        _b = fixture.GetTestServices();
        _config.RemoveFacilityConfig = true;
    }

    public async Task InitializeAsync()
    {
        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var (patientIds, bundles) = FhirBundleGenerator.Generate(Output, 1, 1000, "RegenPatient", GenerationSeed);
        _generatedBundles = bundles;

        if (_config.PatientIds.Count == 0)
            _config.PatientIds = patientIds;

        await GeneratedFhirDataSnapshotWriter.WriteIfChangedAsync(
            Output,
            nameof(RegenerateReportTest),
            GenerationSeed,
            _config.PatientIds,
            bundles);

        await FhirDataLoader.WaitForServerAsync(Output);
        await FhirDataLoader.LoadTransactionBundlesFromJsonAsync(Output, bundles);

        await ValidationApi.InitializeArtifactsAsync();
        await ValidationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine("Cleaning up...\n");

        if (_config.RemoveFacilityConfig)
            await FacilityApi.DeleteAsync(_facilityId);

        if (AutomationCfg.CleanupTestData)
            FhirDataLoader.ExpungeEverything(Output);
    }

    [Fact]
    [Trait("Category", "RegenerateReportTest")]
    public async Task ExecuteRegenerateReportTest()
    {
        var measureLoader = new MeasureLoader(_b.MeasureEvalClient, _b.SdkValidationClient, Output, _config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        await FacilityApi.CreateAsync(_facilityId, measureId);
        await NormalizationApi.CreateConfigAsync(_facilityId);
        await QueryConfigApi.CreateQueryPlanAsync(_facilityId, measureId, "Epic");
        await QueryConfigApi.CreateQueryConfigAsync(_facilityId);

        var sourceReportId = await GenerateCensusBackedAdhocAsync(_facilityId, measureId);
        var sourceSubmitted = await _b.CreateReportApi(_config).CheckSubmissionStatusAsync(sourceReportId);
        Assert.True(sourceSubmitted, $"Source report {sourceReportId} was not submitted.");

        var regeneratedReportId = await RegenerateAsync(_facilityId, sourceReportId);

        await using var diagnostics = new BackgroundDiagnosticsMonitor(
            Output,
            _b.LokiScraper,
            AutomationCfg,
            _config.PatientIds.Count,
            forwardInternalLogsToOutput: false);
        await using var watcher = DiagnosticsEventWatcher.Start(diagnostics, Output);

        await diagnostics.StartAsync(_facilityId, regeneratedReportId);
        var regeneratedSubmitted = await _b.CreateReportApi(_config).CheckSubmissionStatusAsync(regeneratedReportId, diagnostics);
        await diagnostics.StopAsync();
        await watcher.StopAsync();

        var pipelineSnapshot = _b.CreatePipelineSnapshot();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, regeneratedReportId);

        Assert.True(regeneratedSubmitted,
            $"Expected regenerated report {regeneratedReportId} to be submitted but it was not.");

        var reportApi = _b.CreateReportApi(_config);
        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, regeneratedReportId);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected regenerated report to include manifest.ndjson but it was not");

        foreach (var patientId in _config.PatientIds)
        {
            Assert.True(downloadedResources.ContainsKey($"patient-{patientId}.ndjson"),
                $"Expected regenerated report to include patient-{patientId}.ndjson but it was not");
        }

        await _b.CreateReportValidator().ValidateAllAsync(_facilityId, regeneratedReportId, measureId, _config.PatientIds);
        await _b.CreateDataAcqValidator().ValidateAllAsync(_facilityId, regeneratedReportId, measureId, _config.PatientIds);
        await _b.CreateNormalizationValidator().ValidateAllAsync(_facilityId);
        await _b.CreateTenantValidator().ValidateAllAsync(_facilityId, measureId);
        await _b.CreateValidationResultsValidator().ValidateAllAsync(_facilityId, regeneratedReportId, _config.PatientIds, _config.LokiScrapeWindow);
    }

    private async Task<string> GenerateCensusBackedAdhocAsync(string facilityId, string measureId)
    {
        var request = new AdHocReportRequest
        {
            BypassSubmission = false,
            StartDate = DateTime.SpecifyKind(DateTime.Parse(_config.StartDate, CultureInfo.InvariantCulture), DateTimeKind.Utc),
            EndDate = DateTime.UtcNow.AddMinutes(2),
            ReportTypes = [measureId],
            PatientIds = []
        };

        Output.WriteLine("Generating source ad-hoc report (census-backed patient resolution)...");
        var (status, payload) = await _b.ReportClient.GenerateAdhocReportAsync(facilityId, request);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.ReportId);

        return payload.ReportId.ToString();
    }

    private async Task<string> RegenerateAsync(string facilityId, string sourceReportId)
    {
        Output.WriteLine($"Regenerating report from source reportId={sourceReportId}...");

        var request = new RestRequest($"facility/{facilityId}/RegenerateReport", Method.Post)
            .AddJsonBody(new RegenerateReportRequest
            {
                ReportId = sourceReportId,
                BypassSubmission = false
            });

        var response = await _b.AdminBffClient.ExecuteAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(response.Content));

        var payload = JsonSerializer.Deserialize<GenerateAdhocReportResponseApiModel>(response.Content!);
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.ReportId);

        return payload.ReportId.ToString();
    }
}
