using Automation.UI.Models;
using Automation.UI.Services.Persistence;
using LantanaGroup.Automation;
using LantanaGroup.Link.Automation.Link.Configuration;
using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;
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
    private readonly QueryPlanTemplateResolver _queryPlanResolver;
    private readonly NormalizationSuiteResolver _normalizationSuiteResolver;
    private readonly OrganizationResourceMapTemplateResolver _organizationResourceMapResolver;
    private readonly DashboardStatsAggregator _dashboardAggregator;
    private readonly RunExecutor _runExecutor;
    private readonly ILivePatientEventInjector _liveInjector;
    private readonly IPatientConfigurationStore _patientConfigurationStore;
    private readonly IMeasureTemplateStore _measureTemplateStore;
    private readonly ConcurrentDictionary<Guid, MutableRunState> _runs = new();

    public AutomationRunManager(
        IHubContext<RunHub> hub,
        IOptions<AutomationConfig> automationConfig,
        ILogger<AutomationRunManager> logger,
        IServiceProvider hostServices,
        IConfiguration configuration,
        RunSnapshotOrchestrator orchestrator,
        ISnapshotStore snapshotStore,
        IQueryPlanTemplateStore queryPlanTemplateStore,
        INormalizationStore normalizationStore,
        IOrganizationResourceMapTemplateStore organizationResourceMapTemplateStore,
        ImportedBundleExecutionResolver importedBundleResolver,
        LantanaGroup.Automation.Generation.IGeneratedPatientTemplateCache generatedTemplateCache,
        GeneratedTemplateCacheVersionStore generatedTemplateVersionStore,
        ILivePatientEventInjector liveInjector,
        IPatientConfigurationStore patientConfigurationStore,
        IMeasureTemplateStore measureTemplateStore)
    {
        _hub = hub;
        _automationConfig = automationConfig.Value;
        _logger = logger;
        _hostServices = hostServices;
        _orchestrator = orchestrator;
        _snapshotStore = snapshotStore;
        _queryPlanResolver = new QueryPlanTemplateResolver(queryPlanTemplateStore);
        _normalizationSuiteResolver = new NormalizationSuiteResolver(normalizationStore);
        _organizationResourceMapResolver = new OrganizationResourceMapTemplateResolver(organizationResourceMapTemplateStore);
        _dashboardAggregator = new DashboardStatsAggregator(snapshotStore);
        _liveInjector = liveInjector;
        _patientConfigurationStore = patientConfigurationStore;
        _measureTemplateStore = measureTemplateStore;
        _runExecutor = new RunExecutor(
            _automationConfig,
            _hostServices,
            _snapshotStore,
            _orchestrator,
            _queryPlanResolver,
            _normalizationSuiteResolver,
            _organizationResourceMapResolver,
            importedBundleResolver,
            generatedTemplateCache,
            generatedTemplateVersionStore,
            configuration,
            _logger,
            liveInjector);
    }

    public async Task<Guid> StartAsync(StartScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var resolved = StartScenarioRequestResolver.Resolve(request);
        resolved = await MeasureTemplateRunBinder.AttachBundlesAsync(resolved, _measureTemplateStore, cancellationToken);
        var options = await PatientConfigurationHydrator.HydrateAsync(
            resolved,
            _patientConfigurationStore,
            cancellationToken);

        var runNameOverride = string.IsNullOrWhiteSpace(request.ScenarioName) ? null : request.ScenarioName.Trim();
        var state = new MutableRunState(runId, request.ScenarioId, request.Scenario, options, runNameOverride, request.RunConfigurationJson);
        _runs[runId] = state;

        await PersistRunInputAsync(runId, request);

        await PersistRunSummaryAsync(state);

        state.ExecutionTask = Task.Run(async () =>
        {
            try
            {
                var output = new RunAutomationOutput(message => WriteLog(state, message));
                var callbacks = new RunExecutor.ExecutorCallbacks(
                    Output: output,
                    BroadcastStatus: () => BroadcastStatus(state),
                    PersistRunSummary: () => PersistRunSummaryAsync(state));

                await _runExecutor.ExecuteAsync(state, callbacks, state.RunCancellation.Token);
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

        return runId;
    }

    private async Task PersistRunInputAsync(Guid runId, StartScenarioRequest request)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var bundleIds = request.ImportedPatientBundles
                ?.Where(b => b.UploadedBundleId.HasValue)
                .Select(b => b.UploadedBundleId!.Value)
                .Distinct()
                .ToList() ?? [];

            await _snapshotStore.UpsertRunInputAsync(new AutomationRunInputSnapshot
            {
                RunId = runId,
                ScenarioId = request.ScenarioId,
                ScenarioName = request.ScenarioName,
                RunConfigurationJson = request.RunConfigurationJson,
                ImportedBundleIds = bundleIds,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist run input snapshot for {RunId}.", runId);
        }
    }

    public async Task<bool> CancelRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        // â”€â”€ Path 1: zombie run (exists in the store but no in-memory state) â”€â”€â”€â”€â”€â”€â”€â”€
        // _runs is lost on every app restart, so any run that was Running when the
        // previous process died is a zombie: still flagged Running in Mongo, but
        // with no execution task to signal. Returning false here was the reason the
        // UI's Cancel button appeared to do nothing after a docker restart.
        if (!_runs.TryGetValue(runId, out var state))
            return await CancelZombieRunAsync(runId, cancellationToken);

        if (!state.Status.IsCancellable())
            return false;

        // â”€â”€ Path 2: live in-process run â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Flip the in-memory state, broadcast, persist. BroadcastStatus calls
        // PersistRunSummaryAsync internally, so the row is marked Cancelled in Mongo
        // before we return â€” the UI will see the updated status on its next refresh
        // regardless of how long cleanup takes.
        state.CancelRequested = true;
        state.Status = AutomationRunStatus.Cancelled;
        state.Error = "Cancelled by user.";
        state.FinishedAt = DateTimeOffset.UtcNow;
        _liveInjector.CloseSession(runId);
        await BroadcastStatus(state);

        try
        {
            state.RunCancellation.Cancel();
        }
        catch
        {
            // best effort
        }

        // Run the downstream cleanup workflow (FHIR purge, config teardown, report
        // soft-delete) as a background task. Holding the HTTP request for 10s + N
        // cross-service calls is what made Cancel feel hung to users; the run is
        // already marked Cancelled at this point, so the UI doesn't need to wait.
        _ = Task.Run(() => CleanupCancelledRunInBackgroundAsync(state));

        return true;
    }

    /// <summary>
    /// Cancels a run whose in-memory state no longer exists in this process.
    /// Writes a minimal Cancelled summary directly through the snapshot store so
    /// the dashboard updates correctly and the orchestrator stops polling.
    /// Cleanup of downstream service state isn't attempted here because the facility
    /// and report IDs captured on the persisted summary are the only handles we have
    /// and may be missing if the run died before they were assigned.
    /// </summary>
    private async Task<bool> CancelZombieRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var summary = await _snapshotStore.GetRunSummaryAsync(runId, cancellationToken);
        if (summary == null)
            return false;

        if (!summary.Status.IsCancellable())
            return false;

        summary.Status = AutomationRunStatus.Cancelled;
        summary.Error = "Cancelled by user (no active execution in this process).";
        summary.FinishedAt = DateTimeOffset.UtcNow;

        await _snapshotStore.UpsertRunSummaryAsync(summary, summary.FacilityId, summary.ReportId, cancellationToken);
        await _snapshotStore.CompleteRunAsync(runId, duration: null, ct: cancellationToken);
        await _orchestrator.CompleteRunAsync(runId);

        await _hub.Clients.Group(runId.ToString()).SendAsync("status", summary, cancellationToken);
        await _hub.Clients.Group(RunHub.DashboardGroup).SendAsync("dashboardUpdate", summary, cancellationToken);

        _logger.LogInformation(
            "Cancelled zombie run {RunId} (no in-memory state). Downstream service cleanup skipped; operator may need to reset facility {FacilityId} manually if it was partially provisioned.",
            runId, summary.FacilityId);

        return true;
    }

    private async Task CleanupCancelledRunInBackgroundAsync(MutableRunState state)
    {
        try
        {
            if (state.ExecutionTask != null)
            {
                // Let the running pipeline observe the cancellation token and unwind
                // cleanly before we start clawing back its outputs.
                await Task.WhenAny(state.ExecutionTask, Task.Delay(TimeSpan.FromSeconds(10)));
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
                ?? new FhirDataLoader(_automationConfig.FhirServerBase, _automationConfig.FhirServerOAuth, _automationConfig.FhirServerBasicAuth);

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
                CancellationToken.None);

            await _orchestrator.CompleteRunAsync(state.RunId);
            await PersistRunSummaryAsync(state);
            output.WriteLine("Cancellation cleanup complete.");
        }
        catch (Exception ex)
        {
            // A failure in background cleanup must never crash the host. The run is
            // already marked Cancelled in the store from BroadcastStatus above; this
            // only affects downstream service state that an operator may need to
            // inspect/clean up manually.
            _logger.LogError(ex, "Background cancellation cleanup failed for run {RunId}.", state.RunId);
        }
    }

    public async Task<AutomationRunIndexViewModel> GetRunsPageAsync(int pageNumber = 1, int pageSize = 20, string? sortBy = null, bool sortDescending = true, CancellationToken cancellationToken = default)
    {
        var page = await _snapshotStore.GetRunsPageAsync(pageNumber, pageSize, sortBy, sortDescending, cancellationToken);
        return new AutomationRunIndexViewModel
        {
            Runs = page.Items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount,
            // Echo the *normalized* sort back so the view can render its
            // active-column indicator and build correct sort-toggle URLs.
            // Empty/unrecognized sortBy resolves to "createdAt".
            SortBy = NormalizeSortBy(sortBy),
            SortDescending = sortDescending,
        };
    }

    private static string NormalizeSortBy(string? sortBy) =>
        (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "runname"      => "runName",
            "patientcount" => "patientCount",
            "seed"         => "seed",
            "status"       => "status",
            "finishedat"   => "finishedAt",
            "createdat"    => "createdAt",
            _              => "createdAt",
        };

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
        // Always read from Mongo â€” the poller writes domain data there,
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

        var isFinal = status.IsTerminal();

        try
        {
            // Build snapshot from store-cached domain data (zero API calls).
            var builder = new PipelineSummarySnapshotBuilder(async (scheduleId, fId) =>
            {
                var schedule = await SafeGetDomainAsync<PipelineDataReader.ReportScheduleInfo>(runId, "schedule", cancellationToken);
                var entries = await SafeGetDomainAsync<List<PipelineDataReader.ReportEntryInfo>>(runId, "entries", cancellationToken) ?? [];
                var populations = await SafeGetDomainAsync<List<PipelineDataReader.ReportPopulationInfo>>(runId, "populations", cancellationToken) ?? [];
                var acquisitionSummary = await SafeGetDomainAsync<PipelineDataReader.AcquisitionSummaryInfo>(runId, "acquisitionSummary", cancellationToken);
                var acquisitionLogs = await SafeGetDomainAsync<List<PipelineDataReader.AcquisitionLogInfo>>(runId, "acquisitionLogs", cancellationToken) ?? [];
                var measureResources = await SafeGetDomainAsync<List<PipelineDataReader.PatientResourceTypeCount>>(runId, "measureResources", cancellationToken) ?? [];
                var validatorResults = await SafeGetDomainAsync<List<PipelineSummarySnapshotBuilder.ValidatorResultSnapshot>>(runId, "validatorResults", cancellationToken);

                _logger.LogDebug(
                    "[Snapshot][{RunId}] Domain data: schedule={HasSchedule}, entries={EntryCount}, populations={PopCount}, acqSummary={HasAcqSummary} (logs={AcqLogs}), measureRes={MeasureCount}",
                    runId,
                    schedule != null,
                    entries.Count,
                    populations.Count,
                    acquisitionSummary != null,
                    acquisitionSummary?.TotalLogs ?? 0,
                    measureResources.Count);

                return new PipelineSummarySnapshotBuilder.ResolvedDomainData
                {
                    Schedule = schedule,
                    Entries = entries,
                    Populations = populations,
                    AcquisitionSummary = acquisitionSummary,
                    AcquisitionLogs = acquisitionLogs,
                    MeasureEvalResourceCounts = measureResources,
                    ValidatorResults = validatorResults
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

    public async Task<GenerationManifestSnapshot?> GetGenerationManifestAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await SafeGetDomainAsync<GenerationManifestSnapshot>(runId, "generationManifest", cancellationToken);
    }

    public async Task<AbsUploadSnapshot?> GetAbsUploadSnapshotAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await SafeGetDomainAsync<AbsUploadSnapshot>(runId, "absUpload", cancellationToken);
    }

    public Task<RunDashboardStats> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot live in-memory runs so the aggregator can merge any that
        // haven't yet been persisted (brand-new runs round-trip through Mongo
        // on the next BroadcastStatus tick).
        var inMemory = _runs.Values.Select(ToSummary).ToList();
        return _dashboardAggregator.BuildAsync(inMemory, cancellationToken);
    }

    public async Task<PatientStateEvent> InjectAdmitAsync(
        Guid runId,
        string? patientId,
        string source,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        EnsureLiveWindowOpen(runId);
        try
        {
            return await _liveInjector.AdmitAsync(runId, patientId, source, notes, cancellationToken);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live admit failed for run {RunId}.", runId);
            throw new LiveInjectionException(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<PatientStateEvent> InjectDischargeAsync(
        Guid runId,
        string patientId,
        string source,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        EnsureLiveWindowOpen(runId);
        try
        {
            return await _liveInjector.DischargeAsync(runId, patientId, source, notes, cancellationToken);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live discharge failed for run {RunId}.", runId);
            throw new LiveInjectionException(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public Task<IReadOnlyList<PatientStateEvent>> GetLiveEventsAsync(Guid runId, CancellationToken cancellationToken = default)
        => _liveInjector.GetEventsAsync(runId, cancellationToken);

    public async Task<LivePatientPoolEntry> GenerateLivePoolPatientAsync(
        Guid runId,
        string source,
        CancellationToken cancellationToken = default)
    {
        _ = source;
        EnsureLiveWindowOpen(runId);
        try
        {
            return await _liveInjector.GeneratePoolPatientAsync(runId, source, cancellationToken);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live generate failed for run {RunId}.", runId);
            throw new LiveInjectionException(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<LivePatientPoolEntry> UploadLivePoolPatientAsync(
        Guid runId,
        string content,
        string? fileName,
        string source,
        CancellationToken cancellationToken = default)
    {
        _ = source;
        EnsureLiveWindowOpen(runId);
        try
        {
            return await _liveInjector.UploadPoolPatientAsync(runId, content, fileName, source, cancellationToken);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live upload failed for run {RunId}.", runId);
            throw new LiveInjectionException(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public async Task<LivePatientPoolEntry> ReferenceLivePoolPatientAsync(
        Guid runId,
        string patientId,
        string source,
        CancellationToken cancellationToken = default)
    {
        _ = source;
        EnsureLiveWindowOpen(runId);
        try
        {
            return await _liveInjector.ReferencePoolPatientAsync(runId, patientId, source, cancellationToken);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Live reference failed for run {RunId}.", runId);
            throw new LiveInjectionException(ex.Message, StatusCodes.Status500InternalServerError);
        }
    }

    public Task<LivePatientStateSnapshot> GetLivePatientStateAsync(Guid runId, CancellationToken cancellationToken = default)
        => _liveInjector.GetStateAsync(runId, cancellationToken);

    private void EnsureLiveWindowOpen(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var state) || state.Status != AutomationRunStatus.LiveWindowOpen)
            throw new LiveInjectionException("Live window is not accepting injections.", StatusCodes.Status409Conflict);
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
        await _hub.Clients.Group(RunHub.DashboardGroup).SendAsync("dashboardUpdate", ToSummary(state));
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

            summary.RunConfigurationJson = null;
            await _snapshotStore.UpsertRunSummaryAsync(summary, facilityId, reportId);
        }
        catch (Exception ex)
        {
            // Logged at Error: when this throws, the run exists only in-memory and
            // never appears in the Recent Runs table (which is a pure Mongo read
            // with no in-memory merge). Silent Warning-level logging previously
            // masked exactly that failure mode.
            _logger.LogError(ex,
                "Failed to persist run summary for {RunId}. The run will be visible " +
                "on the dashboard KPIs (via the in-memory merge) but will NOT appear " +
                "in the Recent Runs table until a successful upsert lands.",
                state.RunId);
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
                PatientCount = (state.Options.PatientProfiles is { Count: > 0 }
                    ? state.Options.PatientProfiles.Count
                    : state.Options.PatientCount)
                    + state.Options.ImportedPatientIds.Count
                    + state.Options.ImportedPatientBundles.Count,
                ResourcesPerPatient = state.Options.ResourcesPerPatient,
                Seed = state.Options.Seed,
                IsMetricsRun = state.Options.IsMetricsRun,
                RunConfigurationJson = state.RunConfigurationJson,
                Status = state.Status,
                CreatedAt = state.CreatedAt,
                StartedAt = state.StartedAt,
                FinishedAt = state.FinishedAt,
                Error = state.Error,
                FacilityId = state.FacilityId,
                ReportId = state.ReportId,
                GeneratedTemplateCacheVersionId = state.GeneratedTemplateCacheVersionId,
                GeneratedTemplateCacheVersionNumber = state.GeneratedTemplateCacheVersionNumber,
                GeneratedTemplateCacheScenarioKey = state.GeneratedTemplateCacheScenarioKey,
                GeneratedTemplateSetHash = state.GeneratedTemplateSetHash,
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
}
