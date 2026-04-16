using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation;
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
using System.Text.Json;

namespace Automation.UI.Services;

public class AutomationRunManager : IAutomationRunManager
{
    private readonly IHubContext<RunHub> _hub;
    private readonly AutomationConfig _automationConfig;
    private readonly ILogger<AutomationRunManager> _logger;
    private readonly IServiceProvider _hostServices;
    private readonly RunSnapshotOrchestrator _orchestrator;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IQueryPlanTemplateStore _queryPlanTemplateStore;
    private readonly ConcurrentDictionary<Guid, MutableRunState> _runs = new();

    public AutomationRunManager(
        IHubContext<RunHub> hub,
        IOptions<AutomationConfig> automationConfig,
        ILogger<AutomationRunManager> logger,
        IServiceProvider hostServices,
        RunSnapshotOrchestrator orchestrator,
        ISnapshotStore snapshotStore,
        IQueryPlanTemplateStore queryPlanTemplateStore)
    {
        _hub = hub;
        _automationConfig = automationConfig.Value;
        _logger = logger;
        _hostServices = hostServices;
        _orchestrator = orchestrator;
        _snapshotStore = snapshotStore;
        _queryPlanTemplateStore = queryPlanTemplateStore;
    }

    public Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var options = ResolveRunOptions(request);
        var runNameOverride = string.IsNullOrWhiteSpace(request.ScenarioName) ? null : request.ScenarioName.Trim();
        var state = new MutableRunState(runId, request.Scenario, options, runNameOverride, request.RunConfigurationJson);
        _runs[runId] = state;

        _ = PersistRunSummaryAsync(state);

        state.ExecutionTask = Task.Run(async () =>
        {
            try
            {
                await ExecuteAsync(state, state.RunCancellation.Token);
            }
            catch (OperationCanceledException) when (state.CancelRequested)
            {
                _logger.LogInformation("Run {RunId} execution cancelled.", state.RunId);
            }
            catch (Exception ex)
            {
                if (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
                {
                    _logger.LogInformation("Run {RunId} threw after cancellation: {Message}", state.RunId, ex.Message);
                    return;
                }

                _logger.LogError(ex, "Unhandled exception in run {RunId}", state.RunId);
                state.Status = AutomationRunStatus.Failed;
                state.Error = ex.Message;
                state.FinishedAt = DateTimeOffset.UtcNow;
                await BroadcastStatus(state);
            }
        }, CancellationToken.None);

        return Task.FromResult(runId);
    }

    public async Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!_runs.TryGetValue(runId, out var state))
            return false;

        if (state.Status is not AutomationRunStatus.Queued and not AutomationRunStatus.Running)
            return false;

        state.CancelRequested = true;
        state.Status = AutomationRunStatus.Cancelled;
        state.Error = "Cancelled by user.";
        state.FinishedAt = DateTimeOffset.UtcNow;
        await BroadcastStatus(state);

        try
        {
            state.RunCancellation.Cancel();
        }
        catch
        {
            // best effort
        }

        if (state.ExecutionTask != null)
        {
            try
            {
                await Task.WhenAny(state.ExecutionTask, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            }
            catch
            {
                // best effort
            }
        }

        var output = new RunAutomationOutput(message => WriteLog(state, message));
        output.WriteLine("Cancellation requested. Running cancellation cleanup workflow...");

        using var scope = _hostServices.CreateScope();
        var dataAcqClient = scope.ServiceProvider.GetRequiredService<IDataAcquisitionServiceClient>();
        var reportClient = scope.ServiceProvider.GetRequiredService<IReportServiceClient>();
        var facilityClient = scope.ServiceProvider.GetRequiredService<IFacilityServiceClient>();
        var normalizationClient = scope.ServiceProvider.GetRequiredService<INormalizationServiceClient>();
        var queryDispatchClient = scope.ServiceProvider.GetRequiredService<IQueryDispatchServiceClient>();
        var fhirDataLoader = state.FhirDataLoader
            ?? new FhirDataLoader(_automationConfig.ExternalFhirServerBase, _automationConfig.FhirServerOAuth, _automationConfig.FhirServerBasicAuth);

        await RunCleanupHelper.CleanupCancelledRunAsync(
            facilityClient,
            normalizationClient,
            dataAcqClient,
            queryDispatchClient,
            reportClient,
            fhirDataLoader,
            output,
            state.FacilityId,
            state.ReportId,
            cancellationToken);

        await _orchestrator.CompleteRunAsync(runId);
        await PersistRunSummaryAsync(state);
        output.WriteLine("Cancellation cleanup complete.");

        return true;
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
            if (summary.Status is not AutomationRunStatus.Succeeded and not AutomationRunStatus.Failed and not AutomationRunStatus.Cancelled)
                return false;

            _runs.TryRemove(runId, out _);
        }
        else
        {
            summary = await _snapshotStore.GetRunSummaryAsync(runId, cancellationToken);
            if (summary == null)
                return false;
            if (summary.Status is not AutomationRunStatus.Succeeded and not AutomationRunStatus.Failed and not AutomationRunStatus.Cancelled)
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

        var isFinal = status is AutomationRunStatus.Succeeded or AutomationRunStatus.Failed or AutomationRunStatus.Cancelled;

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
            state.FhirDataLoader = fhirDataLoader;
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
                var selectedMeasures = (IReadOnlyList<ProfiledMeasureType>)state.Options.SelectedMeasures;
                var qualAllCount = profiles.Count(p => p.QualifiesForAll(selectedMeasures));
                var nqAllCount = profiles.Count(p => p.QualifiesForNone(selectedMeasures));
                var mixedCount = profiles.Count - qualAllCount - nqAllCount;
                output.WriteLine($"Using measure-eligibility profiles: {qualAllCount} qualifying-all, {nqAllCount} non-qualifying-all, {mixedCount} mixed");

                (patientIds, bundles) = FhirBundleGenerator.GenerateWithProfiles(
                    output,
                    selectedMeasures,
                    profiles,
                    state.Options.ResourcesPerPatient,
                    state.Options.Prefix,
                    state.Options.Seed,
                    generationConfig);

                expectedSubmittedPatientIds = patientIds
                    .Where((_, idx) => idx < profiles.Count && profiles[idx].QualifiesForAny(selectedMeasures))
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

            // Resolve the query plan template (null = use built-in defaults).
            var queryPlanResolution = await ResolveQueryPlanAsync(state.Options.QueryPlanTemplateId);
            var queryPlanInput = queryPlanResolution.Input;
            if (!string.IsNullOrWhiteSpace(queryPlanResolution.Name))
                output.WriteLine($"Using query plan: {queryPlanResolution.Name}");

            await FacilitySetupHelper.EnsureFacilityAsync(
                services.GetRequiredService<IFacilityServiceClient>(),
                output, facilityId, measureIds);
            await FacilitySetupHelper.EnsureNormalizationConfigAsync(
                services.GetRequiredService<INormalizationServiceClient>(),
                output, facilityId);
            await FacilitySetupHelper.EnsureQueryPlansAsync(
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                output, facilityId, measureIds, "Epic", queryPlanInput);
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

            // ?? RegenerateReport: the first report is just a prerequisite.
            // Now trigger regeneration and track the *new* report through the full pipeline.
            if (state.Options.ReportMethod == ReportMethod.RegenerateReport)
            {
                output.WriteLine("???????????????????????????????????????????????????????????????");
                output.WriteLine("Initial report submitted. Beginning REGENERATION phase...");
                output.WriteLine("???????????????????????????????????????????????????????????????");

                // Flush stale domain data so the regenerated report starts fresh.
                services.GetRequiredService<PipelineDataReader>().InvalidateCache();

                var regeneratedReportId = await reportHelper.RegenerateReportAsync(facilityId, reportId);
                reportId = regeneratedReportId;
                lock (state.Sync)
                {
                    state.ReportId = reportId;
                }

                // Re-register the run with the new report ID so pollers track the regenerated report.
                await _orchestrator.UpdateRunAsync(state.RunId, facilityId, reportId, cancellationToken);
                await PersistRunSummaryAsync(state);

                output.WriteLine($"Tracking regenerated report: {reportId}");

                await using var regenDiagnostics = new BackgroundDiagnosticsMonitor(
                    output,
                    lokiScraper,
                    _automationConfig,
                    scenarioConfig.PatientIds.Count,
                    pollInterval: diagnosticsPollInterval,
                    forwardInternalLogsToOutput: true,
                    pipelineReader: services.GetRequiredService<PipelineDataReader>(),
                    expectsDataAcquisition: false);

                await regenDiagnostics.StartAsync(facilityId, reportId);
                var regenSubmitted = await reportHelper.CheckSubmissionStatusAsync(reportId, scenarioConfig, regenDiagnostics);
                await regenDiagnostics.StopAsync();

                if (!regenSubmitted)
                    throw new InvalidOperationException($"Expected regenerated report with id {reportId} to be submitted but it was not.");

                output.WriteLine("Regenerated report submitted successfully.");
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

            // Regeneration reuses prior data acquisition — no new DA logs exist for the regenerated report.
            var expectDataAcquisitionData = state.Options.ReportMethod != ReportMethod.RegenerateReport;

            // Build the concrete generation manifest when profiles are available.
            GenerationManifest? generationManifest = null;
            if (state.Options.PatientProfiles is { Count: > 0 })
            {
                generationManifest = GenerationManifest.Build(
                    patientIds,
                    bundles,
                    state.Options.PatientProfiles,
                    (IReadOnlyList<ProfiledMeasureType>)state.Options.SelectedMeasures);
                generationManifest.MeasureIds = measureIds;
                var effectiveQueryPlanInput = queryPlanInput ?? QueryPlanDefaults.GetDefaultAsInput();
                generationManifest.AcquiredResourceTypes = QueryPlanDefaults.GetAcquiredResourceTypes(effectiveQueryPlanInput);
                generationManifest.ParameterQueryResourceTypes = QueryPlanDefaults.GetParameterQueryResourceTypes(effectiveQueryPlanInput);
                generationManifest.SimulatedAcquiredResourceKeysByPatient = QueryPlanAcquisitionSimulator.SimulateAcquiredKeysByPatient(
                    patientIds,
                    bundles,
                    effectiveQueryPlanInput,
                    scenarioConfig.StartDate,
                    scenarioConfig.EndDate);
                generationManifest.CqlReferencedResourceTypes = CqlResourceTypeExtractor.ExtractForMeasures(state.Options.SelectedMeasures);
            }

            await reportAbsValidator.ValidateAllAsync(
                internalAbsResources,
                expectedSubmittedPatientIds,
                measureIds,
                scenarioConfig.StartDate,
                scenarioConfig.EndDate,
                facilityId,
                reportId,
                bundles,
                expectedManifestPatientListIds: expectedAllPatientIds,
                expectDataAcquisitionData: expectDataAcquisitionData,
                manifest: generationManifest);

            await reportValidator.ValidateAllAsync(
                facilityId,
                reportId,
                measureIds,
                expectedAllPatientIds,
                expectedSubmittedPatientIds: expectedSubmittedPatientIds,
                manifest: generationManifest);
            await dataAcqValidator.ValidateAllAsync(facilityId, reportId, measureIds[0], expectedAllPatientIds, expectDataAcquisitionData: expectDataAcquisitionData);
            await normalizationValidator.ValidateAllAsync(facilityId);
            await tenantValidator.ValidateAllAsync(facilityId, measureId);
            await validationResultsValidator.ValidateAllAsync(facilityId, reportId, expectedAllPatientIds, scenarioConfig.LokiScrapeWindow);

            await RunCleanupHelper.CleanupAfterRunAsync(
                scenarioConfig,
                services.GetRequiredService<IFacilityServiceClient>(),
                services.GetRequiredService<INormalizationServiceClient>(),
                services.GetRequiredService<IDataAcquisitionServiceClient>(),
                services.GetRequiredService<IQueryDispatchServiceClient>(),
                services.GetRequiredService<IReportServiceClient>(),
                fhirDataLoader,
                output,
                facilityId,
                reportId);

            if (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
                throw new OperationCanceledException("Run was cancelled.");

            state.Status = AutomationRunStatus.Succeeded;
            state.FinishedAt = DateTimeOffset.UtcNow;
            await _orchestrator.CompleteRunAsync(state.RunId);
            await BroadcastStatus(state);
            WriteLog(state, "Run completed successfully.");
        }
        catch (OperationCanceledException) when (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
        {
            _logger.LogInformation("Run {RunId} cancellation acknowledged.", state.RunId);
        }
        catch (Exception ex)
        {
            if (state.CancelRequested || state.Status == AutomationRunStatus.Cancelled)
            {
                _logger.LogInformation("Run {RunId} faulted after cancel request: {Message}", state.RunId, ex.Message);
                return;
            }

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
            .AddSingleton(sp => {
                var cfg = sp.GetRequiredService<AutomationConfig>();
                return new FhirDataLoader(cfg.ExternalFhirServerBase, cfg.FhirServerOAuth, cfg.FhirServerBasicAuth);
            })
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
            AutomationScenarioKind.AdhocReportTest => "adhoc-report-submission.zip",
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
            CleanupServiceData = options.CleanupServiceData,
            CleanupFhirData = options.CleanupFhirData,
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
            AutomationScenarioKind.AdhocReportTest => new ResolvedRunOptions(1, 1000, "AdhocPatient", 20260326, 3, 0, 30, false, true, defaultMeasures, [], []),
            AutomationScenarioKind.MultiPatientTest => new ResolvedRunOptions(1000, 100, "MultiPatient", 20260328, 3, 0, 30, false, true, defaultMeasures, [], []),
            AutomationScenarioKind.MegaPatientTest => new ResolvedRunOptions(FhirBundleGenerator.DefaultPatientCount, FhirBundleGenerator.DefaultResourcesPerPatient, "MegaPatient", 20260327, 3, 0, 30, false, true, defaultMeasures, [], []),
            AutomationScenarioKind.Custom => new ResolvedRunOptions(10, 250, "CustomPatient", 20260329, 3, 0, 30, false, true, defaultMeasures, [], []),
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

        var effectiveMeasures = request.SelectedMeasures is { Count: > 0 }
            ? request.SelectedMeasures
            : defaults.SelectedMeasures;

        var cohorts = request.PatientCohorts is { Count: > 0 }
            ? request.PatientCohorts
            : ExtractCohortsFromJson(request.RunConfigurationJson, effectiveMeasures)
              ?? [
                  PatientCohortDefinition.AllQualifying(
                      effectiveMeasures,
                      request.PatientCount ?? defaults.PatientCount,
                      request.ResourcesPerPatient ?? defaults.ResourcesPerPatient,
                      request.ResourcesPerPatient ?? defaults.ResourcesPerPatient)
              ];

        // Resolve measures: prefer SelectedMeasures list, fall back to single SelectedMeasure, then defaults
        var measures = request.SelectedMeasures is { Count: > 0 }
            ? request.SelectedMeasures
            : request.SelectedMeasure.HasValue
                ? [request.SelectedMeasure.Value]
                : effectiveMeasures;

        // Always expand profiles from cohorts — cohorts are the single source of truth.
        profiles = ExpandProfilesFromCohorts(cohorts, request.Seed ?? defaults.Seed);

        return defaults with
        {
            PatientCount = request.PatientCount ?? defaults.PatientCount,
            ResourcesPerPatient = request.ResourcesPerPatient ?? defaults.ResourcesPerPatient,
            Prefix = prefix,
            Seed = request.Seed ?? defaults.Seed,
            PollingIntervalSeconds = 3,
            MaxPollingDurationMinutes = 0,
            LokiScrapeWindowMinutes = 30,
            CleanupServiceData = request.CleanupServiceData ?? defaults.CleanupServiceData,
            CleanupFhirData = request.CleanupFhirData ?? defaults.CleanupFhirData,
            SelectedMeasures = measures,
            PatientProfiles = profiles,
            PatientCohorts = cohorts,
            ReportMethod = request.ReportMethod,
            QueryPlanTemplateId = request.QueryPlanTemplateId
        };
    }

    private static List<PatientProfile> ExpandProfilesFromCohorts(IReadOnlyList<PatientCohortDefinition> cohorts, int seed)
        => PatientCohortDefinition.ExpandProfiles(cohorts, seed);

    /// <summary>
    /// Extracts cohort definitions from the <c>RunConfigurationJson</c> blob.
    /// The JSON is the full scenario payload produced by the UI and contains a
    /// <c>patientCohorts</c> array.  Returns null when parsing fails or the
    /// array is empty so the caller can fall back to defaults.
    /// </summary>
    private static List<PatientCohortDefinition>? ExtractCohortsFromJson(string? json, List<ProfiledMeasureType> measures)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("patientCohorts", out var cohortsEl)
                || cohortsEl.ValueKind != JsonValueKind.Array
                || cohortsEl.GetArrayLength() == 0)
                return null;

            var cohorts = new List<PatientCohortDefinition>();

            foreach (var item in cohortsEl.EnumerateArray())
            {
                var count = item.TryGetProperty("patientCount", out var pc) ? pc.GetInt32() : 0;
                var min = item.TryGetProperty("resourcesPerPatientMin", out var rmin) ? rmin.GetInt32() : 50;
                var max = item.TryGetProperty("resourcesPerPatientMax", out var rmax) ? rmax.GetInt32() : min;

                var eligibilities = new Dictionary<ProfiledMeasureType, MeasureEligibility>();
                if (item.TryGetProperty("measureEligibilities", out var meEl) && meEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in meEl.EnumerateObject())
                    {
                        if (Enum.TryParse<ProfiledMeasureType>(prop.Name, ignoreCase: true, out var mt))
                        {
                            MeasureEligibility eligibility;
                            if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                eligibility = Enum.TryParse<MeasureEligibility>(prop.Value.GetString(), ignoreCase: true, out var parsed)
                                    ? parsed
                                    : MeasureEligibility.Qualifying;
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                eligibility = prop.Value.GetInt32() == 0
                                    ? MeasureEligibility.Qualifying
                                    : MeasureEligibility.NonQualifying;
                            }
                            else
                            {
                                eligibility = MeasureEligibility.Qualifying;
                            }
                            eligibilities[mt] = eligibility;
                        }
                    }
                }

                // Ensure every selected measure has an entry (default to qualifying).
                foreach (var m in measures)
                {
                    eligibilities.TryAdd(m, MeasureEligibility.Qualifying);
                }

                var scenarioIds = new List<string>();
                if (item.TryGetProperty("eligibleClinicalScenarioIds", out var sEl) && sEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in sEl.EnumerateArray())
                    {
                        var sv = s.GetString();
                        if (!string.IsNullOrEmpty(sv))
                            scenarioIds.Add(sv);
                    }
                }

                cohorts.Add(new PatientCohortDefinition
                {
                    PatientCount = count,
                    MeasureEligibilities = eligibilities,
                    EligibleClinicalScenarioIds = scenarioIds,
                    ResourcesPerPatientMin = min,
                    ResourcesPerPatientMax = max
                });
            }

            return cohorts.Count > 0 ? cohorts : null;
        }
        catch
        {
            return null;
        }
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
                RunName = state.RunNameOverride ?? GetRunName(state.Scenario, state.Options.SelectedMeasures),
                Scenario = state.Scenario,
                SelectedMeasure = string.Join(", ", state.Options.SelectedMeasures.Select(ProfiledMeasureCatalog.GetDisplayName)),
                PatientCount = state.Options.PatientProfiles is { Count: > 0 }
                    ? state.Options.PatientProfiles.Count
                    : state.Options.PatientCount,
                ResourcesPerPatient = state.Options.ResourcesPerPatient,
                Seed = state.Options.Seed,
                RunConfigurationJson = state.RunConfigurationJson,
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

    private sealed class MutableRunState(
        Guid runId,
        AutomationScenarioKind scenario,
        ResolvedRunOptions options,
        string? runNameOverride,
        string? runConfigurationJson)
    {
        public object Sync { get; } = new();
        public Guid RunId { get; } = runId;
        public AutomationScenarioKind Scenario { get; } = scenario;
        public ResolvedRunOptions Options { get; } = options;
        public string? RunNameOverride { get; } = runNameOverride;
        public string? RunConfigurationJson { get; } = runConfigurationJson;
        public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public string? FacilityId { get; set; }
        public string? ReportId { get; set; }
        public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Queued;
        public string? Error { get; set; }
        public List<string> Logs { get; } = [];
        public CancellationTokenSource RunCancellation { get; } = new();
        public bool CancelRequested { get; set; }
        public Task? ExecutionTask { get; set; }
        public FhirDataLoader? FhirDataLoader { get; set; }
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
        bool CleanupServiceData,
        bool CleanupFhirData,
        List<ProfiledMeasureType> SelectedMeasures,
        List<PatientProfile> PatientProfiles,
        List<PatientCohortDefinition> PatientCohorts,
        ReportMethod ReportMethod = ReportMethod.Adhoc,
        Guid? QueryPlanTemplateId = null);

    /// <summary>
    /// Resolves the query plan template for a run. Returns null to use built-in defaults.
    /// </summary>
    private async Task<QueryPlanResolution> ResolveQueryPlanAsync(Guid? templateId)
    {
        QueryPlanTemplate? template = null;

        if (templateId.HasValue)
            template = await _queryPlanTemplateStore.GetByIdAsync(templateId.Value);

        // Fall back to the default template if no explicit one was provided or it wasn't found.
        template ??= await _queryPlanTemplateStore.GetDefaultAsync();

        if (template == null)
            return new QueryPlanResolution(null, "Built-in Default");

        return new QueryPlanResolution(ToQueryPlanInput(template), template.Name);
    }

    private static QueryPlanInput ToQueryPlanInput(QueryPlanTemplate template) => new()
    {
        EhrDescription = template.EhrDescription,
        LookBack = template.LookBack,
        InitialQueries = template.InitialQueries.Select(ToQueryPlanQueryEntry).ToList(),
        SupplementalQueries = template.SupplementalQueries.Select(ToQueryPlanQueryEntry).ToList()
    };

    private static QueryPlanQueryEntry ToQueryPlanQueryEntry(QueryEntry src) => new()
    {
        ResourceType = src.ResourceType,
        QueryConfigType = src.QueryConfigType,
        OperationType = src.OperationType,
        Paged = src.Paged,
        Parameters = src.Parameters.Select(p => new QueryPlanParameterEntry
        {
            ParameterType = p.ParameterType,
            Name = p.Name,
            Variable = p.Variable,
            Format = p.Format,
            Literal = p.Literal,
            Resource = p.Resource,
            PagedValue = p.PagedValue
        }).ToList()
    };

    private sealed record QueryPlanResolution(QueryPlanInput? Input, string Name);
}
