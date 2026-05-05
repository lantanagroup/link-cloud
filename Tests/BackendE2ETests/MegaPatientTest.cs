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
/// Stress/volume test that generates a synthetic patient with ~5,000
/// FHIR resources and runs it through the full ad-hoc reporting pipeline.
/// </summary>
public sealed class MegaPatientTest : IAsyncLifetime, IClassFixture<BackendE2ETestFixture>
{
    private const int GenerationSeed = 20260327;

    private static readonly TestScenarioConfig Config = TestConfig.MegaPatientTestConfig;

    private readonly IServiceProvider _sp;
    private readonly string _facilityId = $"MegaPatient-{Guid.NewGuid():N}";
    private List<ProfiledMeasureType> _measures = [];
    private GenerationManifest? _generationManifest;
    private string? _reportId;

    private AutomationConfig AutomationCfg => _sp.GetRequiredService<AutomationConfig>();
    private ConsoleAutomationOutput Output => _sp.GetRequiredService<ConsoleAutomationOutput>();
    private FhirDataLoader FhirDataLoader => _sp.GetRequiredService<FhirDataLoader>();

    public MegaPatientTest(BackendE2ETestFixture fixture)
    {
        _sp = fixture.ServiceProvider;
    }

    public async Task InitializeAsync()
    {
        _sp.GetRequiredService<PipelineDataReader>().InvalidateCache();

        Output.WriteLine($"Using deterministic generation seed: {GenerationSeed}");
        var measures = new List<ProfiledMeasureType> { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        _measures = measures;
        var cohorts = new List<PatientCohortDefinition>
        {
            PatientCohortDefinition.AllQualifying(measures, patientCount: 1, resourcesMin: 5000, resourcesMax: 5000)
        };
        var profiles = PatientCohortDefinition.ExpandProfiles(cohorts, GenerationSeed);

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
        {
            Config.PatientIds = pipelineResult.PatientIds;
        }

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
    [Trait("Category", "MegaPatientTest")]
    public async Task ExecuteMegaPatientTest()
    {
        // Step 1: Load measure definition into MeasureEval and Validation.
        var measureLoader = new MeasureLoader(
            _sp.GetRequiredService<IMeasureEvalServiceClient>(),
            _sp.GetRequiredService<IValidationServiceClient>(),
            Output, Config);
        await measureLoader.LoadAsync();

        var measureId = measureLoader.MeasureId
            ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

        Output.WriteLine($"MeasureId: {measureId}");
        Output.WriteLine($"Patients : {Config.PatientIds.Count}");

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

        var generationManifest = _generationManifest
            ?? throw new InvalidOperationException("Generation manifest was not produced by the pipeline.");
        generationManifest.MeasureIds = [measureId];
        var queryPlanInput = QueryPlanBuilder.GetDefaultAsInput();
        generationManifest.AcquiredResourceTypes = QueryPlanBuilder.GetAcquiredResourceTypes(queryPlanInput);
        generationManifest.ParameterQueryResourceTypes = QueryPlanBuilder.GetParameterQueryResourceTypes(queryPlanInput);
        generationManifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(_measures);

        await _sp.GetRequiredService<ReportAbsManifestValidator>().ValidateAllAsync(
            internalAbsResources,
            Config.PatientIds,
            measureId,
            Config.StartDate,
            Config.EndDate,
            _facilityId,
            reportId,
            generatedBundles: null,
            manifest: generationManifest);

        // Step 9-10: Strict database validation.
        await _sp.GetRequiredService<ReportDatabaseValidator>().ValidateAllAsync(_facilityId, reportId, measureId, Config.PatientIds);
        await _sp.GetRequiredService<DataAcquisitionDatabaseValidator>().ValidateAllAsync(_facilityId, reportId, measureId, Config.PatientIds);
        await _sp.GetRequiredService<NormalizationDatabaseValidator>().ValidateAllAsync(_facilityId);
        await _sp.GetRequiredService<TenantDatabaseValidator>().ValidateAllAsync(_facilityId, measureId);

        // Step 11: Validation results exception check (API + Validation service logs).
        await _sp.GetRequiredService<ValidationResultsValidator>().ValidateAllAsync(_facilityId, reportId, Config.PatientIds, Config.LokiScrapeWindow);
    }
}

