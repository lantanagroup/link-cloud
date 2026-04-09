using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Automation.Link.Services;
using LantanaGroup.Link.Automation.Link.Validation;
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
    private readonly RunSnapshotOrchestrator _orchestrator;
    private readonly ISnapshotStore _snapshotStore;
    private readonly ConcurrentDictionary<Guid, MutableRunState> _runs = new();

    public AutomationRunManager(
        IHubContext<RunHub> hub,
        IOptions<AutomationConfig> automationConfig,
        ILogger<AutomationRunManager> logger,
        IServiceProvider hostServices,
        RunSnapshotOrchestrator orchestrator,
        ISnapshotStore snapshotStore)
    {
        _hub = hub;
        _automationConfig = automationConfig.Value;
        _logger = logger;
        _hostServices = hostServices;
        _orchestrator = orchestrator;
        _snapshotStore = snapshotStore;
    }

    public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var options = ResolveRunOptions(request);
        var state = new MutableRunState(runId, request.Scenario, options);
        _runs[runId] = state;

        _ = PersistRunSummaryAsync(state);

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(state, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in run {RunId}", state.RunId);
                state.Status = AutomationRunStatus.Failed;
                state.Error = ex.Message;
                state.FinishedAt = DateTimeOffset.UtcNow;
                await BroadcastStatus(state);
            }
        }, CancellationToken.None);
        return Task.FromResult(runId);
    }

    public async Task<AutomationRunIndexViewModel> GetRunsPageAsync(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var page = await _snapshotStore.GetRunsPageAsync(pageNumber, pageSize, cancellationToken);
        return new AutomationRunIndexViewModel
        {
            Runs = page.Items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };
    }

    public async Task<AutomationRunSummary?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (_runs.TryGetValue(runId, out var state))
            return ToSummary(state);

        var summary = await _snapshotStore.GetRunSummaryAsync(runId, cancellationToken);
        if (summary == null)
            return null;

        summary.Logs = await _snapshotStore.GetLogsAsync(runId, cancellationToken);
        return summary;
    }

    public async Task<bool> DeleteRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        AutomationRunSummary? summary;

        if (_runs.TryGetValue(runId, out var state))
        {
            summary = ToSummary(state);
            if (summary.Status is not AutomationRunStatus.Succeeded and not AutomationRunStatus.Failed)
                return false;

            _runs.TryRemove(runId, out _);
        }
        else
        {
            summary = await _snapshotStore.GetRunSummaryAsync(runId, cancellationToken);
            if (summary == null)
                return false;
            if (summary.Status is not AutomationRunStatus.Succeeded and not AutomationRunStatus.Failed)
                return false;
        }

        await _snapshotStore.DeleteRunAsync(runId, cancellationToken);
        return true;
    }

    public async Task<PipelineSummarySnapshotBuilder.PipelineSummarySnapshot?> GetPipelineSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        // Always read from Mongo — the poller writes domain data there,
        // and logs are persisted as they're written. One data flow, no branching.
        var summary = await _snapshotStore.GetRunSummaryAsync(runId, cancellationToken);

        // For a live run that hasn't been persisted yet, fall back to in-memory for basic fields.
        string? facilityId;
        string? reportId;
        List<string> logs;
        AutomationRunStatus status;

        if (_runs.TryGetValue(runId, out var state))
        {
            lock (state.Sync)
            {
                facilityId = state.FacilityId;
                reportId = state.ReportId;
                logs = state.Logs.ToList();
            }
            status = state.Status;
        }
        else if (summary != null)
        {
            facilityId = summary.FacilityId;
            reportId = summary.ReportId;
            logs = await _snapshotStore.GetLogsAsync(runId, cancellationToken);
            status = summary.Status;
        }
        else
        {
            return null;
        }

        var isFinal = status is AutomationRunStatus.Succeeded or AutomationRunStatus.Failed;

        try
        {
            // Build snapshot from store-cached domain data (zero API calls).
            var builder = new PipelineSummarySnapshotBuilder(async (scheduleId, fId) =>
            {
                var schedule = await SafeGetDomainAsync<PipelineDataReader.ReportScheduleInfo>(runId, "schedule", cancellationToken);
                var entries = await SafeGetDomainAsync<List<PipelineDataReader.ReportEntryInfo>>(runId, "entries", cancellationToken) ?? [];
                var populations = await SafeGetDomainAsync<List<PipelineDataReader.ReportPopulationInfo>>(runId, "populations", cancellationToken) ?? [];
                var acquisitionSummary = await SafeGetDomainAsync<PipelineDataReader.AcquisitionSummaryInfo>(runId, "acquisitionSummary", cancellationToken);
                var measureResources = await SafeGetDomainAsync<List<PipelineDataReader.PatientResourceTypeCount>>(runId, "measureResources", cancellationToken) ?? [];
                var validationResources = await SafeGetDomainAsync<List<PipelineDataReader.PatientResourceTypeCount>>(runId, "validationResources", cancellationToken) ?? [];

                _logger.LogDebug(
                    "[Snapshot][{RunId}] Domain data: schedule={HasSchedule}, entries={EntryCount}, populations={PopCount}, acqSummary={HasAcqSummary} (logs={AcqLogs}), measureRes={MeasureCount}, valRes={ValCount}",
                    runId,
                    schedule != null,
                    entries.Count,
                    populations.Count,
                    acquisitionSummary != null,
                    acquisitionSummary?.TotalLogs ?? 0,
                    measureResources.Count,
                    validationResources.Count);

                return new PipelineSummarySnapshotBuilder.ResolvedDomainData
                {
                    Schedule = schedule,
                    Entries = entries,
                    Populations = populations,
                    AcquisitionSummary = acquisitionSummary,
                    MeasureEvalResourceCounts = measureResources,
                    ReportResourceCounts = validationResources
                };
            });

            var snapshot = await builder.BuildAsync(facilityId, reportId, logs, cancellationToken);
            snapshot.IsFinal = isFinal;
            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline snapshot build failed for run {RunId} (facility={FacilityId}, report={ReportId})", runId, facilityId, reportId);

            try
            {
                var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [Snapshot] ERROR: {ex.GetType().Name}: {ex.Message}";
                await _snapshotStore.AppendLogsAsync(runId, [line], CancellationToken.None);
            }
            catch
            {
                // best effort only
            }

            return new PipelineSummarySnapshotBuilder.PipelineSummarySnapshot
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                FacilityId = facilityId,
                ReportId = reportId,
                IsFinal = isFinal
            };
        }
    }

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
            output.WriteLine($"Measure context: {string.Join(", ", state.Options.SelectedMeasures.Select(m => $"{ProfiledMeasureCatalog.GetDisplayName(m)} ({m})"))}");
            output.WriteLine($"Generation config: patients={state.Options.PatientCount}, resourcesPerPatient={state.Options.ResourcesPerPatient}, prefix={state.Options.Prefix}, seed={state.Options.Seed}");

            List<string> patientIds;
            List<(string Name, string Json)> bundles;
            List<string> expectedSubmittedPatientIds;

            // Use the first measure for generation context (profile-driven generation picks
            // the most restrictive measure — patients qualifying for all measures must meet
            // the criteria of each). For multi-measure, GenerateWithProfiles handles the union.
            var primaryMeasure = state.Options.SelectedMeasures[0];
            var generationConfig = ResolveFhirGenerationConfig(_automationConfig);

            if (state.Options.PatientProfiles is { Count: > 0 })
            {
                var profiles = state.Options.PatientProfiles;
                output.WriteLine($"Using measure-eligibility profiles: {profiles.Count(p => p.Eligibility == MeasureEligibility.Qualifying)} qualifying, {profiles.Count(p => p.Eligibility == MeasureEligibility.NonQualifying)} non-qualifying");

                if (state.Options.SelectedMeasures.Count > 1)
                {
                    (patientIds, bundles) = FhirBundleGenerator.GenerateWithProfiles(
                        output,
                        (IReadOnlyList<ProfiledMeasureType>)state.Options.SelectedMeasures,
                        profiles,
                        state.Options.ResourcesPerPatient,
                        state.Options.Prefix,
                        state.Options.Seed,
                        generationConfig);
                }
                else
                {
                    (patientIds, bundles) = FhirBundleGenerator.GenerateWithProfiles(
                        output,
                        primaryMeasure,
                        profiles,
                        state.Options.ResourcesPerPatient,
                        state.Options.Prefix,
                        state.Options.Seed,
                        generationConfig);
                }

                expectedSubmittedPatientIds = patientIds
                    .Where((_, idx) => idx < profiles.Count && profiles[idx].Eligibility == MeasureEligibility.Qualifying)
                    .ToList();
            }
            else
            {
                (patientIds, bundles) = FhirBundleGenerator.Generate(
                    output, state.Options.PatientCount, state.Options.ResourcesPerPatient, state.Options.Prefix, state.Options.Seed,
                    generationConfig);

                expectedSubmittedPatientIds = patientIds.ToList();
            }

            if (scenarioConfig.PatientIds.Count == 0)
                scenarioConfig.PatientIds = patientIds;

            var expectedAllPatientIds = scenarioConfig.PatientIds;

            await fhirDataLoader.WaitForServerAsync(output);

            if (state.Options.CleanupTestData)
            {
                output.WriteLine("Cleanup is enabled; expunging existing FHIR test data before loading generated bundles...");
                fhirDataLoader.ExpungeEverything(output);
            }

            await fhirDataLoader.LoadTransactionBundlesFromJsonAsync(output, bundles);

            await validationHelper.InitializeArtifactsAsync();
            await validationHelper.InitializeCategoriesAsync();

            var measureLoader = new MeasureLoader(measureEvalClient, sdkValidationClient, output, scenarioConfig);
            await measureLoader.LoadAllAsync();
            var measureIds = measureLoader.MeasureIds;
            if (measureIds.Count == 0)
                throw new InvalidOperationException("MeasureLoader did not produce any MeasureIds");
            var measureId = measureIds[0];

            var facilityId = $"{state.Scenario}-{state.RunId:N}".Substring(0, Math.Min(48, $"{state.Scenario}-{state.RunId:N}".Length));
            lock (state.Sync)
            {
                state.FacilityId = facilityId;
            }

            await FacilitySetupHelper.EnsureFacilityAsync(
                services.GetRequiredService<IFacilityServiceClient>(),
                output, facilityId, measureIds);
            await FacilitySetupHelper.EnsureNormalizationConfigAsync(
                services.GetRequiredService<INormalizationServiceClient>(),
                output, facilityId);
            await FacilitySetupHelper.EnsureQueryPlansAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output, facilityId, measureIds, "Epic");
            await FacilitySetupHelper.EnsureQueryConfigAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<AutomationConfig>(),
                output, facilityId);
            await FacilitySetupHelper.EnsureQueryDispatchConfigAsync(
                services.GetRequiredService<IQueryDispatchServiceClient>(),
                output,
                facilityId);

            var reportId = await reportHelper.GenerateReportAsync(facilityId, measureIds, scenarioConfig);
            lock (state.Sync)
            {
                state.ReportId = reportId;
            }

            // Register with orchestrator so store-backed pollers start automatically.
            await _orchestrator.RegisterRunAsync(state.RunId, facilityId, reportId);

            var diagnosticsPollInterval = scenarioConfig.PatientIds.Count >= 500
                ? TimeSpan.FromSeconds(15)
                : TimeSpan.FromSeconds(5);

            await using (var diagnostics = new BackgroundDiagnosticsMonitor(
                output,
                lokiScraper,
                _automationConfig,
                scenarioConfig.PatientIds.Count,
                pollInterval: diagnosticsPollInterval,
                forwardInternalLogsToOutput: true,
                pipelineReader: services.GetRequiredService<PipelineDataReader>()))
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

            // Flush stale cache from diagnostics polling so validators read authoritative data.
            services.GetRequiredService<PipelineDataReader>().InvalidateCache();

            await reportAbsValidator.ValidateAllAsync(
                internalAbsResources,
                expectedSubmittedPatientIds,
                measureIds,
                scenarioConfig.StartDate,
                scenarioConfig.EndDate,
                facilityId,
                reportId,
                bundles,
                expectedManifestPatientListIds: expectedAllPatientIds);

            await reportValidator.ValidateAllAsync(
                facilityId,
                reportId,
                measureIds,
                expectedAllPatientIds,
                expectedSubmittedPatientIds: expectedSubmittedPatientIds);
            await dataAcqValidator.ValidateAllAsync(facilityId, reportId, measureIds[0], expectedAllPatientIds);
            await normalizationValidator.ValidateAllAsync(facilityId);
            await tenantValidator.ValidateAllAsync(facilityId, measureId);
            await validationResultsValidator.ValidateAllAsync(facilityId, reportId, expectedAllPatientIds, scenarioConfig.LokiScrapeWindow);

            if (scenarioConfig.RemoveFacilityConfig)
                await FacilitySetupHelper.CleanupFacilityAsync(
                    services.GetRequiredService<IFacilityServiceClient>(),
                    services.GetRequiredService<INormalizationServiceClient>(),
                    services.GetRequiredService<IDataAcquisitionServiceClient>(),
                    services.GetRequiredService<IQueryDispatchServiceClient>(),
                    output, facilityId);

            if (state.Options.CleanupTestData)
            {
                await FacilitySetupHelper.SoftDeleteRunDataAsync(
                    services.GetRequiredService<IReportServiceClient>(),
                    services.GetRequiredService<IDataAcquisitionServiceClient>(),
                    services.GetRequiredService<IQueryDispatchServiceClient>(),
                    output,
                    facilityId,
                    reportId);

                fhirDataLoader.ExpungeEverything(output);
            }

            state.Status = AutomationRunStatus.Succeeded;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await _orchestrator.CompleteRunAsync(state.RunId);
            await BroadcastStatus(state);
            WriteLog(state, "Run completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed", state.RunId);
            state.Status = AutomationRunStatus.Failed;
            state.Error = ex.Message;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await _orchestrator.CompleteRunAsync(state.RunId);
            await BroadcastStatus(state);
            WriteLog(state, $"Run failed: {ex.Message}");
        }
    }

    private static FhirGenerationConfig ResolveFhirGenerationConfig(AutomationConfig automationConfig)
    {
        var includeLowValueOptionalReferences = automationConfig.FhirGeneration?.IncludeLowValueOptionalReferences ?? true;
        var distribution = automationConfig.FhirGeneration?.ResourceDistribution;
        if (distribution == null || distribution.Count == 0)
            return new FhirGenerationConfig
            {
                IncludeLowValueOptionalReferences = includeLowValueOptionalReferences
            };

        return new FhirGenerationConfig
        {
            IncludeLowValueOptionalReferences = includeLowValueOptionalReferences,
            ResourceDistribution = new Dictionary<string, double>(distribution, StringComparer.OrdinalIgnoreCase)
        };
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
            .AddSingleton(sp => new LantanaGroup.Link.Automation.Link.Helpers.DatabaseConnectionFactory(sp.GetRequiredService<AutomationConfig>().Database))
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

        var bundleLocations = options.SelectedMeasures
            .Select(ProfiledMeasureCatalog.GetBundleLocation)
            .ToList();

        return new TestScenarioConfig
        {
            MeasureBundleLocation = bundleLocations.Count > 0 ? bundleLocations[0] : "",
            AdditionalMeasureBundleLocations = bundleLocations.Count > 1 ? bundleLocations.Skip(1).ToList() : [],
            StartDate = "2023-01-01T00:00:00Z",
            EndDate = "2023-12-31T23:59:59Z",
            PatientIds = [],
            RemoveFacilityConfig = options.RemoveFacilityConfig,
            PollingIntervalSeconds = options.PollingIntervalSeconds,
            MaxPollingDurationMinutes = options.MaxPollingDurationMinutes,
            DownloadFileName = downloadFileName,
            LokiScrapeWindowMinutes = options.LokiScrapeWindowMinutes
        };
    }

    private ResolvedRunOptions ResolveRunOptions(StartScenarioRequest request)
    {
        var defaultMeasures = new List<ProfiledMeasureType> { ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation };
        var defaults = request.Scenario switch
        {
            AutomationScenarioKind.SmokeTest => new ResolvedRunOptions(1, 1000, "SmokePatient", 20260326, 3, 0, 30, true, false, defaultMeasures, []),
            AutomationScenarioKind.MultiPatientTest => new ResolvedRunOptions(1000, 100, "MultiPatient", 20260328, 3, 0, 30, true, false, defaultMeasures, []),
            AutomationScenarioKind.MegaPatientTest => new ResolvedRunOptions(FhirBundleGenerator.DefaultPatientCount, FhirBundleGenerator.DefaultResourcesPerPatient, "MegaPatient", 20260327, 3, 0, 30, true, false, defaultMeasures, []),
            AutomationScenarioKind.Custom => new ResolvedRunOptions(10, 250, "CustomPatient", 20260329, 3, 0, 30, true, false, defaultMeasures, []),
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

        // Resolve measures: prefer SelectedMeasures list, fall back to single SelectedMeasure, then defaults
        var measures = request.SelectedMeasures is { Count: > 0 }
            ? request.SelectedMeasures
            : request.SelectedMeasure.HasValue
                ? [request.SelectedMeasure.Value]
                : defaults.SelectedMeasures;

        return defaults with
        {
            PatientCount = request.PatientCount ?? defaults.PatientCount,
            ResourcesPerPatient = request.ResourcesPerPatient ?? defaults.ResourcesPerPatient,
            Prefix = prefix,
            Seed = request.Seed ?? defaults.Seed,
            PollingIntervalSeconds = 3,
            MaxPollingDurationMinutes = 0,
            LokiScrapeWindowMinutes = 30,
            RemoveFacilityConfig = request.RemoveFacilityConfig ?? defaults.RemoveFacilityConfig,
            CleanupTestData = request.CleanupTestData ?? defaults.CleanupTestData,
            SelectedMeasures = measures,
            PatientProfiles = profiles
        };
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

        // Persist to store (fire-and-forget, best effort)
        _ = Task.Run(async () =>
        {
            try { await _snapshotStore.AppendLogsAsync(state.RunId, [line]); }
            catch { /* log persistence is best-effort */ }
        });
    }

    private async Task BroadcastStatus(MutableRunState state)
    {
        await _hub.Clients.Group(state.RunId.ToString()).SendAsync("status", ToSummary(state));
        await PersistRunSummaryAsync(state);
    }

    private async Task PersistRunSummaryAsync(MutableRunState state)
    {
        try
        {
            AutomationRunSummary summary;
            string? facilityId;
            string? reportId;

            lock (state.Sync)
            {
                summary = ToSummary(state);
                facilityId = state.FacilityId;
                reportId = state.ReportId;
            }

            await _snapshotStore.UpsertRunSummaryAsync(summary, facilityId, reportId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to persist run summary for {RunId}", state.RunId);
        }
    }

    /// <summary>
    /// Reads a single domain snapshot from the store, returning null on any failure
    /// so one broken domain doesn't take down the entire snapshot.
    /// </summary>
    private async Task<T?> SafeGetDomainAsync<T>(Guid runId, string domain, CancellationToken ct) where T : class
    {
        try
        {
            return (await _snapshotStore.GetDomainAsync<T>(runId, domain, ct))?.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Snapshot][{RunId}] Failed to read domain '{Domain}' (type={Type})", runId, domain, typeof(T).Name);

            try
            {
                var line = $"[{DateTimeOffset.Now:HH:mm:ss}] [Snapshot][{domain}] ERROR: {ex.GetType().Name}: {ex.Message}";
                await _snapshotStore.AppendLogsAsync(runId, [line], CancellationToken.None);
            }
            catch
            {
                // best effort only
            }

            return null;
        }
    }

    private static AutomationRunSummary ToSummary(MutableRunState state)
    {
        lock (state.Sync)
        {
            return new AutomationRunSummary
            {
                RunId = state.RunId,
                RunName = GetRunName(state.Scenario, state.Options.SelectedMeasures),
                Scenario = state.Scenario,
                SelectedMeasure = string.Join(", ", state.Options.SelectedMeasures.Select(ProfiledMeasureCatalog.GetDisplayName)),
                PatientCount = state.Options.PatientProfiles is { Count: > 0 }
                    ? state.Options.PatientProfiles.Count
                    : state.Options.PatientCount,
                ResourcesPerPatient = state.Options.ResourcesPerPatient,
                Seed = state.Options.Seed,
                Status = state.Status,
                CreatedAt = state.CreatedAt,
                StartedAt = state.StartedAt,
                FinishedAt = state.FinishedAt,
                Error = state.Error,
                FacilityId = state.FacilityId,
                ReportId = state.ReportId,
                Logs = state.Logs.ToList()
            };
        }
    }

    private static string GetRunName(AutomationScenarioKind scenario, List<ProfiledMeasureType> selectedMeasures)
    {
        if (scenario != AutomationScenarioKind.Custom)
            return scenario.ToString();

        if (selectedMeasures.Count > 1)
            return "Custom-MultiMeasure";

        return selectedMeasures.FirstOrDefault() switch
        {
            ProfiledMeasureType.NhsnGlycemicControlHypoglycemicInitialPopulation => "Custom-HYPO",
            ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation => "Custom-Daily-ACH",
            ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation => "Custom-Monthly-ACH",
            _ => "Custom"
        };
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
        public string? FacilityId { get; set; }
        public string? ReportId { get; set; }
        public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Queued;
        public string? Error { get; set; }
        public List<string> Logs { get; } = [];
    }

    private sealed class NullAutomationOutput : IAutomationOutput
    {
        public void WriteLine(string message)
        {
        }

        public void WriteLine(string format, params object[] args)
        {
        }
    }

    private record ResolvedRunOptions(
        int PatientCount,
        int ResourcesPerPatient,
        string Prefix,
        int Seed,
        int PollingIntervalSeconds,
        int MaxPollingDurationMinutes,
        int LokiScrapeWindowMinutes,
        bool RemoveFacilityConfig,
        bool CleanupTestData,
        List<ProfiledMeasureType> SelectedMeasures,
        List<PatientProfile> PatientProfiles);
}
