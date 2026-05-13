using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
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
public sealed class MultiMeasureTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260420;

    private static readonly TestScenarioConfig Config = TestConfig.BuildScenarioConfig(
        "MULTI_MEASURE_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 3,
        defaultMaxPollingDurationMinutes: 7,
        defaultLokiScrapeWindowMinutes: 10);

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"MultiMeasure-{Guid.NewGuid():N}";
    private List<string> _expectedSubmittedPatientIds = [];
    private List<ProfiledMeasureType> _measures = [];
    private GenerationManifest? _generationManifest;
    private string? _reportId;

    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private ConsoleAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();
    private FhirDataLoader FhirDataLoader => _sp.GetRequiredService<FhirDataLoader>();

    public MultiMeasureTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    public async Task InitializeAsync()
    {
        _sp.GetRequiredService<PipelineDataReader>().InvalidateCache();

        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");

        var measures = new List<ProfiledMeasureType>
        {
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
        };
        _measures = measures;

        // Cohort 1: qualifies for both ACH and Hypo (inpatient + diabetic med)
        // Cohort 2: qualifies for ACH only (inpatient, no Hypo med)
        var cohorts = new List<PatientCohortDefinition>
        {
            new()
            {
                PatientCount = 1,
                MeasureEligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                    [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.Qualifying
                },
                EligibleClinicalScenarioIds =
                [
                    ..ClinicalScenarioEligibility.GetEligibleScenarioIds(
                    [
                        ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation,
                        ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation
                    ], MeasureEligibility.Qualifying)
                ],
                ResourcesPerPatientMin = 250,
                ResourcesPerPatientMax = 250
            },
            new()
            {
                PatientCount = 1,
                MeasureEligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>
                {
                    [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying,
                    [ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation] = MeasureEligibility.NonQualifying
                },
                // Match the UI's MultiMeasureTest scenario (ScenarioSeedService): any ACH-qualifying
                // scenario is permitted for this cohort. The MeasureEligibilities dict above tells
                // the generator to suppress Hypo-qualifying resources (insulin, hypoglycemic obs)
                // even when the chosen scenario could otherwise qualify for Hypo.
                EligibleClinicalScenarioIds =
                [
                    ..ClinicalScenarioEligibility.GetEligibleScenarioIds(
                        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
                        MeasureEligibility.Qualifying)
                ],
                ResourcesPerPatientMin = 250,
                ResourcesPerPatientMax = 250
            }
        };
        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, GenerationSeed);

        // Populate additional bundle locations for the Hypo measure
        Config.MeasureBundleLocation = ProfiledMeasureCatalog.GetBundleLocation(
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation);
        Config.AdditionalMeasureBundleLocations =
        [
            ProfiledMeasureCatalog.GetBundleLocation(
                ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation)
        ];

        await FhirDataLoader.WaitForServerAsync(Output);

        var pipelineResult = await FhirGenerationPipeline.GenerateAndUploadAsync(
            Output,
            FhirDataLoader,
            measures,
            profiles,
            totalResourcesPerPatient: profiles[0].ResourcesPerPatient ?? 100,
            generationSeed: GenerationSeed,
            acquisitionSimulation: new FhirGenerationPipeline.AcquisitionSimulationConfig
            {
                QueryPlan = QueryPlanBuilder.GetDefaultAsInput(),
                ClinicalPeriodStart = Config.StartDate,
                ClinicalPeriodEnd = Config.EndDate
            });

        _generationManifest = pipelineResult.Manifest;

        if (Config.PatientIds.Count == 0)
            Config.PatientIds = pipelineResult.PatientIds;

        // Patient 1 qualifies for both ACH + Hypo (submitted for both).
        // Patient 2 qualifies for ACH only (submitted for ACH, not Hypo).
        // Both appear in the ABS submission because each qualifies for at least one measure.
        _expectedSubmittedPatientIds = pipelineResult.PatientIds.ToList();

        Output.WriteLine($"Patient IDs for test: [{string.Join(", ", Config.PatientIds)}]");

        var validationApi = _sp.GetRequiredService<ValidationApiHelper>();
        await validationApi.InitializeArtifactsAsync();
        await validationApi.InitializeCategoriesAsync();
    }

    public async Task DisposeAsync()
    {
        Output.WriteLine("Cleaning up...\n");

        await RunCleanupHelper.CleanupAfterRunAsync(
            Config,
            _sp.GetRequiredService<IFacilityServiceClient>(),
            _sp.GetRequiredService<INormalizationServiceClient>(),
            _sp.GetRequiredService<IDataAcquisitionServiceClient>(),
            _sp.GetRequiredService<IQueryDispatchServiceClient>(),
            _sp.GetRequiredService<IReportServiceClient>(),
            FhirDataLoader,
            Output,
            _facilityId,
            _reportId);
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
        var internalAbsResources = await reportApi.DownloadReportAsync(_facilityId, reportId, Config, external: false);

        Assert.True(downloadedResources.ContainsKey("manifest.ndjson"),
            "Expected report to include manifest.ndjson but it was not");

        // The qualifying patient (index 0) should be in the report.
        var qualifyingPatientId = Config.PatientIds[0];
        Assert.True(downloadedResources.ContainsKey($"patient-{qualifyingPatientId}.ndjson"),
            $"Expected report to include patient-{qualifyingPatientId}.ndjson (qualifying) but it was not");

        Output.WriteLine("Done generating and validating multi-measure report.");

        // Flush stale cache from diagnostics polling so validators read authoritative data.
        dataReader.InvalidateCache();

        var generationManifest = _generationManifest
            ?? throw new InvalidOperationException("Generation manifest was not produced by the pipeline.");
        generationManifest.MeasureIds = measureIds;
        var queryPlanInput = QueryPlanBuilder.GetDefaultAsInput();
        generationManifest.AcquiredResourceTypes = QueryPlanBuilder.GetAcquiredResourceTypes(queryPlanInput);
        generationManifest.ParameterQueryResourceTypes = QueryPlanBuilder.GetParameterQueryResourceTypes(queryPlanInput);
        generationManifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(_measures);

        await _sp.GetRequiredService<ReportAbsManifestValidator>().ValidateAllAsync(
            internalAbsResources,
            _expectedSubmittedPatientIds,
            (IReadOnlyList<string>)measureIds,
            Config.StartDate,
            Config.EndDate,
            _facilityId,
            reportId,
            generatedBundles: null,
            expectedManifestPatientListIds: Config.PatientIds,
            manifest: generationManifest);

        // Step 9: Database validation with all measure IDs.
        await _sp.GetRequiredService<ReportDatabaseValidator>().ValidateAllAsync(
            _facilityId,
            reportId,
            (IReadOnlyList<string>)measureIds,
            Config.PatientIds,
            expectedSubmittedPatientIds: _expectedSubmittedPatientIds,
            manifest: generationManifest);
        await _sp.GetRequiredService<DataAcquisitionDatabaseValidator>().ValidateAllAsync(
            _facilityId, reportId, measureIds[0], Config.PatientIds);
        await _sp.GetRequiredService<NormalizationDatabaseValidator>().ValidateAllAsync(_facilityId);
        await _sp.GetRequiredService<TenantDatabaseValidator>().ValidateAllAsync(_facilityId, measureIds[0]);
        await _sp.GetRequiredService<ValidationResultsValidator>().ValidateAllAsync(
            _facilityId, reportId, Config.PatientIds, Config.LokiScrapeWindow);
    }
}
