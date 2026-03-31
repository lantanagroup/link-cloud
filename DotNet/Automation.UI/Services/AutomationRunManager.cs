using Automation.UI.Models;
using Automation.UI.Services;
using LantanaGroup.Link.Automation;
using LantanaGroup.Link.Automation.Configuration;
using LantanaGroup.Link.Automation.Generation;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Automation.Services;
using LantanaGroup.Link.Automation.Validation;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Sdk.DependencyInjection;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Automation.UI.Services;

public class AutomationRunManager : IAutomationRunManager
{
    private readonly IHubContext<RunHub> _hub;
    private readonly AutomationConfig _automationConfig;
    private readonly ILogger<AutomationRunManager> _logger;
    private readonly IServiceProvider _hostServices;
    private readonly ConcurrentDictionary<Guid, MutableRunState> _runs = new();

    public AutomationRunManager(
        IHubContext<RunHub> hub,
        IOptions<AutomationConfig> automationConfig,
        ILogger<AutomationRunManager> logger,
        IServiceProvider hostServices)
    {
        _hub = hub;
        _automationConfig = automationConfig.Value;
        _logger = logger;
        _hostServices = hostServices;
    }

    public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var options = ResolveRunOptions(request);
        var state = new MutableRunState(runId, request.Scenario, options);
        _runs[runId] = state;

        _ = Task.Run(() => ExecuteAsync(state, cancellationToken), CancellationToken.None);
        return Task.FromResult(runId);
    }

    public IReadOnlyList<AutomationRunSummary> GetRuns() => _runs.Values
        .OrderByDescending(x => x.CreatedAt)
        .Select(ToSummary)
        .ToList();

    public AutomationRunSummary? GetRun(Guid runId)
        => _runs.TryGetValue(runId, out var state) ? ToSummary(state) : null;

    private async Task ExecuteAsync(MutableRunState state, CancellationToken cancellationToken)
    {
        state.Status = AutomationRunStatus.Running;
        state.StartedAt = DateTimeOffset.UtcNow;
        await BroadcastStatus(state);

        var output = new RunAutomationOutput(message => WriteLog(state, message));

        try
        {
            var scenarioConfig = BuildScenarioConfig(state.Scenario, state.Options);

            using var services = BuildRunServiceProvider(output);

            var lokiScraper = services.GetRequiredService<LokiScraper>();
            var fhirDataLoader = services.GetRequiredService<FhirDataLoader>();
            var measureEvalClient = services.GetRequiredService<IMeasureEvalServiceClient>();
            var sdkValidationClient = services.GetRequiredService<IValidationServiceClient>();

            var reportHelper = services.GetRequiredService<ReportApiHelper>();

            var validationHelper = services.GetRequiredService<ValidationApiHelper>();
            var reportValidator = services.GetRequiredService<ReportDatabaseValidator>();
            var reportAbsValidator = services.GetRequiredService<ReportAbsManifestValidator>();
            var dataAcqValidator = services.GetRequiredService<DataAcquisitionDatabaseValidator>();
            var normalizationValidator = services.GetRequiredService<NormalizationDatabaseValidator>();
            var tenantValidator = services.GetRequiredService<TenantDatabaseValidator>();
            var validationResultsValidator = services.GetRequiredService<ValidationResultsValidator>();
            var pipelineSnapshot = services.GetRequiredService<PipelineSnapshot>();

            output.WriteLine($"Starting {state.Scenario} run: {state.RunId}");
            output.WriteLine($"Measure context: {ProfiledMeasureCatalog.GetDisplayName(state.Options.SelectedMeasure)} ({state.Options.SelectedMeasure})");
            output.WriteLine($"Generation config: patients={state.Options.PatientCount}, resourcesPerPatient={state.Options.ResourcesPerPatient}, prefix={state.Options.Prefix}, seed={state.Options.Seed}");

            List<string> patientIds;
            List<(string Name, string Json)> bundles;
            List<string> expectedSubmittedPatientIds;

            if (state.Options.PatientProfiles is { Count: > 0 })
            {
                var profiles = state.Options.PatientProfiles;
                output.WriteLine($"Using measure-eligibility profiles: {profiles.Count(p => p.Eligibility == MeasureEligibility.Qualifying)} qualifying, {profiles.Count(p => p.Eligibility == MeasureEligibility.NonQualifying)} non-qualifying");
                (patientIds, bundles) = FhirBundleGenerator.GenerateWithProfiles(
                    output,
                    state.Options.SelectedMeasure,
                    profiles,
                    state.Options.ResourcesPerPatient,
                    state.Options.Prefix,
                    state.Options.Seed);

                expectedSubmittedPatientIds = patientIds
                    .Where((_, idx) => idx < profiles.Count && profiles[idx].Eligibility == MeasureEligibility.Qualifying)
                    .ToList();
            }
            else
            {
                (patientIds, bundles) = FhirBundleGenerator.Generate(
                    output, state.Options.PatientCount, state.Options.ResourcesPerPatient, state.Options.Prefix, state.Options.Seed);

                expectedSubmittedPatientIds = patientIds.ToList();
            }

            if (scenarioConfig.PatientIds.Count == 0)
                scenarioConfig.PatientIds = patientIds;

            var expectedAllPatientIds = scenarioConfig.PatientIds;

            await fhirDataLoader.WaitForServerAsync(output);
            await fhirDataLoader.LoadTransactionBundlesFromJsonAsync(output, bundles);

            await validationHelper.InitializeArtifactsAsync();
            await validationHelper.InitializeCategoriesAsync();

            var measureLoader = new MeasureLoader(measureEvalClient, sdkValidationClient, output, scenarioConfig);
            await measureLoader.LoadAsync();
            var measureId = measureLoader.MeasureId ?? throw new InvalidOperationException("MeasureLoader did not produce a MeasureId");

            var facilityId = $"{state.Scenario}-{state.RunId:N}".Substring(0, Math.Min(48, $"{state.Scenario}-{state.RunId:N}".Length));

            await FacilitySetupHelper.EnsureFacilityAsync(
                services.GetRequiredService<IFacilityServiceClient>(),
                output, facilityId, measureId);
            await FacilitySetupHelper.EnsureNormalizationConfigAsync(
                services.GetRequiredService<INormalizationServiceClient>(),
                output, facilityId);
            await FacilitySetupHelper.EnsureQueryPlansAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output, facilityId, measureId, "Epic");
            await FacilitySetupHelper.EnsureQueryConfigAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<AutomationConfig>(),
                output, facilityId);

            var reportId = await reportHelper.GenerateReportAsync(facilityId, measureId, scenarioConfig);

            await using (var diagnostics = new BackgroundDiagnosticsMonitor(output, lokiScraper, _automationConfig, scenarioConfig.PatientIds.Count, forwardInternalLogsToOutput: true, pipelineReader: services.GetRequiredService<PipelineDataReader>()))
            {
                await diagnostics.StartAsync(facilityId, reportId);
                var submitted = await reportHelper.CheckSubmissionStatusAsync(reportId, scenarioConfig, diagnostics);
                await diagnostics.StopAsync();

                if (!submitted)
                    throw new InvalidOperationException($"Expected report with id {reportId} to be submitted but it was not.");
            }

            await pipelineSnapshot.WriteFullSnapshotAsync(output, facilityId, reportId);

            var downloadedResources = await reportHelper.DownloadReportAsync(facilityId, reportId, scenarioConfig);
            var internalAbsResources = await reportHelper.DownloadReportAsync(facilityId, reportId, scenarioConfig, external: false);

            if (!downloadedResources.ContainsKey("manifest.ndjson"))
                throw new InvalidOperationException("Expected report to include manifest.ndjson but it was not");

            foreach (var patientId in expectedSubmittedPatientIds)
            {
                if (!downloadedResources.ContainsKey($"patient-{patientId}.ndjson"))
                    throw new InvalidOperationException($"Expected report to include patient-{patientId}.ndjson but it was not");
            }

            var snapshotDir = await WriteGeneratedSnapshotAsync(state, expectedAllPatientIds, bundles);

            await reportAbsValidator.ValidateAllAsync(
                internalAbsResources,
                expectedSubmittedPatientIds,
                measureId,
                scenarioConfig.StartDate,
                scenarioConfig.EndDate,
                facilityId,
                reportId,
                snapshotDir,
                expectedManifestPatientListIds: expectedAllPatientIds);

            await reportValidator.ValidateAllAsync(
                facilityId,
                reportId,
                measureId,
                expectedAllPatientIds,
                expectedSubmittedPatientIds: expectedSubmittedPatientIds);
            await dataAcqValidator.ValidateAllAsync(facilityId, reportId, measureId, expectedAllPatientIds);
            await normalizationValidator.ValidateAllAsync(facilityId);
            await tenantValidator.ValidateAllAsync(facilityId, measureId);
            await validationResultsValidator.ValidateAllAsync(facilityId, reportId, expectedAllPatientIds, scenarioConfig.LokiScrapeWindow);

            if (scenarioConfig.RemoveFacilityConfig)
                await FacilitySetupHelper.CleanupFacilityAsync(
                    services.GetRequiredService<IFacilityServiceClient>(),
                    services.GetRequiredService<INormalizationServiceClient>(),
                    services.GetRequiredService<IDataAcquisitionServiceClient>(),
                    output, facilityId);

            if (state.Options.CleanupTestData)
                fhirDataLoader.ExpungeEverything(output);

            state.Status = AutomationRunStatus.Succeeded;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await BroadcastStatus(state);
            WriteLog(state, "Run completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed", state.RunId);
            state.Status = AutomationRunStatus.Failed;
            state.Error = ex.Message;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await BroadcastStatus(state);
            WriteLog(state, $"Run failed: {ex.Message}");
        }
    }

    private ServiceProvider BuildRunServiceProvider(IAutomationOutput output)
    {
        var services = new ServiceCollection();

        services.AddSingleton(_automationConfig);
        services.AddSingleton(output);

        // Forward host-level configuration into the per-run container
        services.AddSingleton(_hostServices.GetRequiredService<IOptions<ServiceRegistry>>());
        services.AddSingleton(_hostServices.GetRequiredService<IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions>>());
        services.AddSingleton(_hostServices.GetRequiredService<IOptions<LinkTokenServiceSettings>>());
        services.AddSingleton(_hostServices.GetRequiredService<ICreateSystemToken>());

        services.AddLinkSdk();

        services.AddSingleton(sp => new LokiScraper(sp.GetRequiredService<IAutomationOutput>(), sp.GetRequiredService<AutomationConfig>()))
            .AddSingleton(sp => new FhirDataLoader(sp.GetRequiredService<AutomationConfig>().ExternalFhirServerBase, sp.GetRequiredService<AutomationConfig>()))
            .AddSingleton(sp => new DatabaseConnectionFactory(sp.GetRequiredService<AutomationConfig>().Database))
            .AddSingleton<PipelineDataReader>();

        services.AddTransient<ValidationApiHelper>();
        services.AddTransient<ReportApiHelper>();
        services.AddTransient<ReportDatabaseValidator>();
        services.AddTransient<ReportAbsManifestValidator>();
        services.AddTransient<DataAcquisitionDatabaseValidator>();
        services.AddTransient<NormalizationDatabaseValidator>();
        services.AddTransient<TenantDatabaseValidator>();
        services.AddTransient<ValidationResultsValidator>();
        services.AddTransient<PipelineSnapshot>();

        return services.BuildServiceProvider();
    }

    private static TestScenarioConfig BuildScenarioConfig(AutomationScenarioKind scenario, ResolvedRunOptions options)
    {
        var downloadFileName = scenario switch
        {
            AutomationScenarioKind.SmokeTest => "smoke-submission.zip",
            AutomationScenarioKind.MultiPatientTest => "multi-patient-submission.zip",
            AutomationScenarioKind.MegaPatientTest => "mega-patient-submission.zip",
            AutomationScenarioKind.Custom => "custom-submission.zip",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        return new TestScenarioConfig
        {
            MeasureBundleLocation = ProfiledMeasureCatalog.GetBundleLocation(options.SelectedMeasure),
            StartDate = "2023-01-01T00:00:00Z",
            EndDate = "2023-12-31T23:59:59Z",
            PatientIds = [],
            RemoveFacilityConfig = options.RemoveFacilityConfig,
            PollingIntervalSeconds = options.PollingIntervalSeconds,
            MaxRetryCount = options.MaxRetryCount,
            DownloadFileName = downloadFileName,
            LokiScrapeWindowMinutes = options.LokiScrapeWindowMinutes
        };
    }

    private ResolvedRunOptions ResolveRunOptions(StartScenarioRequest request)
    {
        var defaults = request.Scenario switch
        {
            AutomationScenarioKind.SmokeTest => new ResolvedRunOptions(1, 1000, "SmokePatient", 20260326, 3, 60, 5, true, _automationConfig.CleanupTestData, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation, []),
            AutomationScenarioKind.MultiPatientTest => new ResolvedRunOptions(1000, 100, "MultiPatient", 20260328, 3, 60, 5, true, _automationConfig.CleanupTestData, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation, []),
            AutomationScenarioKind.MegaPatientTest => new ResolvedRunOptions(FhirBundleGenerator.DefaultPatientCount, FhirBundleGenerator.DefaultResourcesPerPatient, "MegaPatient", 20260327, 5, 300, 20, true, _automationConfig.CleanupTestData, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation, []),
            AutomationScenarioKind.Custom => new ResolvedRunOptions(10, 250, "CustomPatient", 20260329, 3, 120, 10, true, _automationConfig.CleanupTestData, ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation, []),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Scenario), request.Scenario, null)
        };

        if (request.Scenario != AutomationScenarioKind.Custom)
            return defaults;

        var prefix = string.IsNullOrWhiteSpace(request.PatientPrefix)
            ? defaults.Prefix
            : request.PatientPrefix.Trim();

        var profiles = request.PatientProfiles is { Count: > 0 }
            ? request.PatientProfiles
            : defaults.PatientProfiles;

        return defaults with
        {
            PatientCount = request.PatientCount ?? defaults.PatientCount,
            ResourcesPerPatient = request.ResourcesPerPatient ?? defaults.ResourcesPerPatient,
            Prefix = prefix,
            Seed = request.Seed ?? defaults.Seed,
            PollingIntervalSeconds = request.PollingIntervalSeconds ?? defaults.PollingIntervalSeconds,
            MaxRetryCount = request.MaxRetryCount ?? defaults.MaxRetryCount,
            LokiScrapeWindowMinutes = request.LokiScrapeWindowMinutes ?? defaults.LokiScrapeWindowMinutes,
            RemoveFacilityConfig = request.RemoveFacilityConfig ?? defaults.RemoveFacilityConfig,
            CleanupTestData = request.CleanupTestData ?? defaults.CleanupTestData,
            SelectedMeasure = request.SelectedMeasure ?? defaults.SelectedMeasure,
            PatientProfiles = profiles
        };
    }

    private async Task<string> WriteGeneratedSnapshotAsync(MutableRunState state, IReadOnlyCollection<string> patientIds, List<(string Name, string Json)> bundles)
    {
        var snapshotDir = Path.Combine(Path.GetTempPath(), "automation-ui-snapshots", state.Scenario.ToString(), state.RunId.ToString("N"));
        Directory.CreateDirectory(snapshotDir);

        foreach (var (name, json) in bundles)
        {
            await File.WriteAllTextAsync(Path.Combine(snapshotDir, $"{name}.json"), json);
        }

        await File.WriteAllTextAsync(Path.Combine(snapshotDir, "metadata.txt"),
            $"Seed={state.Options.Seed}{Environment.NewLine}Patients={string.Join(",", patientIds)}{Environment.NewLine}GeneratedAt={DateTimeOffset.UtcNow:O}");

        return snapshotDir;
    }

    private void WriteLog(MutableRunState state, string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss}] {message}";
        lock (state.Sync)
        {
            state.Logs.Add(line);
            if (state.Logs.Count > 4000)
                state.Logs.RemoveRange(0, 500);
        }

        _ = _hub.Clients.Group(state.RunId.ToString()).SendAsync("log", line);
    }

    private Task BroadcastStatus(MutableRunState state)
        => _hub.Clients.Group(state.RunId.ToString()).SendAsync("status", ToSummary(state));

    private static AutomationRunSummary ToSummary(MutableRunState state)
    {
        lock (state.Sync)
        {
            return new AutomationRunSummary
            {
                RunId = state.RunId,
                Scenario = state.Scenario,
                Status = state.Status,
                CreatedAt = state.CreatedAt,
                StartedAt = state.StartedAt,
                FinishedAt = state.FinishedAt,
                Error = state.Error,
                Logs = state.Logs.ToList()
            };
        }
    }

    private sealed class MutableRunState(Guid runId, AutomationScenarioKind scenario, ResolvedRunOptions options)
    {
        public object Sync { get; } = new();
        public Guid RunId { get; } = runId;
        public AutomationScenarioKind Scenario { get; } = scenario;
        public ResolvedRunOptions Options { get; } = options;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Queued;
        public string? Error { get; set; }
        public List<string> Logs { get; } = [];
    }

    private record ResolvedRunOptions(
        int PatientCount,
        int ResourcesPerPatient,
        string Prefix,
        int Seed,
        int PollingIntervalSeconds,
        int MaxRetryCount,
        int LokiScrapeWindowMinutes,
        bool RemoveFacilityConfig,
        bool CleanupTestData,
        ProfiledMeasureType SelectedMeasure,
        List<PatientProfile> PatientProfiles);
}
