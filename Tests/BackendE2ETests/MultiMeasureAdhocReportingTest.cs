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
/// End-to-end test that validates multi-measure ad-hoc reporting.
/// Uses Monthly ACH and Hypoglycemic measures simultaneously, with one qualifying
/// patient (qualifies for both measures) and one non-qualifying patient.
/// The qualifying patient must have an inpatient encounter within the measurement
/// period AND a diabetic medication administration to satisfy both measures.
/// </summary>
public sealed class MultiMeasureAdhocReportingTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260420;

    private static readonly TestScenarioConfig Config = TestConfig.BuildScenarioConfig(
        "MULTI_MEASURE_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 3,
        defaultMaxRetryCount: 140,
        defaultLokiScrapeWindowMinutes: 10);

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"MultiMeasure-{Guid.NewGuid():N}";
    private List<(string Name, string Json)> _generatedBundles = [];
    private string? _reportId;

    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private DualOutputHelper Output => _sp.GetRequiredService<DualOutputHelper>();
    private FhirDataLoader FhirDataLoader => _sp.GetRequiredService<FhirDataLoader>();

    public MultiMeasureAdhocReportingTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    public async Task InitializeAsync()
    {
        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");

        // One qualifying patient (must satisfy BOTH Monthly ACH and Hypo criteria)
        // and one non-qualifying patient.
        var profiles = new List<PatientProfile>
        {
            new(MeasureEligibility.Qualifying),
            new(MeasureEligibility.NonQualifying)
        };

        var measures = new List<ProfiledMeasureType>
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
        };

        var (patientIds, bundles) = FhirBundleGenerator.GenerateWithProfiles(
            Output, (IReadOnlyList<ProfiledMeasureType>)measures, profiles, 250, "MultiMeasurePatient", GenerationSeed);

        _generatedBundles = bundles;

        if (Config.PatientIds.Count == 0)
            Config.PatientIds = patientIds;

        // Populate additional bundle locations for the Hypo measure
        Config.MeasureBundleLocation = ProfiledMeasureCatalog.GetBundleLocation(
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        Config.AdditionalMeasureBundleLocations =
        [
            ProfiledMeasureCatalog.GetBundleLocation(
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)
        ];

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
    [Trait("Category", "MultiMeasureTest")]
    public async Task ExecuteMultiMeasureTest()
    {
        // Step 1: Load both measure definitions into MeasureEval and Validation.
        var measureLoader = new MeasureLoader(
            _sp.GetRequiredService<IMeasureEvalServiceClient>(),
            _sp.GetRequiredService<IValidationServiceClient>(),
            Output, Config);
        await measureLoader.LoadAllAsync();

        var measureIds = measureLoader.MeasureIds;
        Assert.True(measureIds.Count >= 2,
            $"Expected at least 2 measure IDs but got {measureIds.Count}: [{string.Join(", ", measureIds)}]");

        Output.WriteLine($"MeasureIds: [{string.Join(", ", measureIds)}]");
        Output.WriteLine($"Patients : {Config.PatientIds.Count}");

        // Step 2: Create facility with both measures.
        await FacilitySetupHelper.EnsureFacilityAsync(
            _sp.GetRequiredService<IFacilityServiceClient>(), Output, _facilityId, measureIds);

        // Step 3: Create normalization config.
        await FacilitySetupHelper.EnsureNormalizationConfigAsync(
            _sp.GetRequiredService<INormalizationServiceClient>(), Output, _facilityId);

        // Step 4: Create query plans for each measure.
        await FacilitySetupHelper.EnsureQueryPlansAsync(
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(), Output, _facilityId, measureIds, "Epic");

        // Step 5: Create FHIR query config.
        await FacilitySetupHelper.EnsureQueryConfigAsync(
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(), AutomationCfg, Output, _facilityId);
        await FacilitySetupHelper.EnsureQueryDispatchConfigAsync(
            _sp.GetRequiredService<IQueryDispatchServiceClient>(),
            Output,
            _facilityId);

        // Step 6: Generate the ad-hoc report with both measures as report types.
        var reportApi = _sp.GetRequiredService<ReportApiHelper>();
        var reportId = await reportApi.GenerateReportAsync(_facilityId, measureIds, Config);
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

        // Always capture a snapshot before assertions.
        var pipelineSnapshot = _sp.GetRequiredService<PipelineSnapshot>();
        await pipelineSnapshot.WriteFullSnapshotAsync(Output, _facilityId, reportId);

        Assert.True(reportSubmitted,
            $"Expected report with id {reportId} to be submitted but it was not. " +
            $"Check [DIAG] and [Snapshot] output above for root cause details.");

        // Step 8: Download and validate report artifacts.
        var downloadedResources = await reportApi.DownloadReportAsync(_facilityId, reportId, Config);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        // The qualifying patient (index 0) should be in the report.
        var qualifyingPatientId = Config.PatientIds[0];
        Assert.True(downloadedResources.ContainsKey($"patient-{qualifyingPatientId}.ndjson"),
            $"Expected report to include patient-{qualifyingPatientId}.ndjson (qualifying) but it was not");

        Output.WriteLine("Done generating and validating multi-measure report.");

        // Step 9: Database validation with all measure IDs.
        await _sp.GetRequiredService<ReportDatabaseValidator>().ValidateAllAsync(
            _facilityId, reportId, (IReadOnlyList<string>)measureIds, Config.PatientIds);
        await _sp.GetRequiredService<DataAcquisitionDatabaseValidator>().ValidateAllAsync(
            _facilityId, reportId, measureIds[0], Config.PatientIds);
        await _sp.GetRequiredService<NormalizationDatabaseValidator>().ValidateAllAsync(_facilityId);
        await _sp.GetRequiredService<TenantDatabaseValidator>().ValidateAllAsync(_facilityId, measureIds[0]);
        await _sp.GetRequiredService<ValidationResultsValidator>().ValidateAllAsync(
            _facilityId, reportId, Config.PatientIds, Config.LokiScrapeWindow);
    }
}
