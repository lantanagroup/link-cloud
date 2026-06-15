using System.Collections.Concurrent;
using System.Text.Json;
using Automation.UI.Models.ApiHealth;
using Automation.UI.Services.ApiHealth.Seeding;
using Automation.UI.Services.ApiHealth.TestSuites;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services.ApiHealth;

public sealed class ApiHealthExecutionRunManager(
    ApiEndpointRegistry registry,
    IApiHealthSeedOrchestrator seedOrchestrator,
    IApiHealthSeedContextAccessor seedContext,
    IApiHealthRunStore store,
    ILogger<ApiHealthExecutionRunManager> logger)
{
    private const string SanitizedInternalError = "An internal error occurred processing this run.";
    private static readonly TimeSpan CompletedRunRetention = TimeSpan.FromHours(6);
    private const int MaxCompletedRunsToRetain = 200;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ConcurrentDictionary<Guid, RunState> _runs = new();
    private readonly object _sync = new();
    private Guid? _activeRunId;

    public async Task<Guid> StartServiceAsync(string serviceName)
    {
        lock (_sync)
        {
            if (_activeRunId is Guid activeId
                && _runs.TryGetValue(activeId, out var active)
                && !active.Completed)
                return activeId;
        }

        var run = new RunState
        {
            RunId = Guid.NewGuid(),
            Scope = "Service",
            ServiceName = serviceName,
            StartedAt = DateTimeOffset.UtcNow
        };

        _runs[run.RunId] = run;
        lock (_sync)
            _activeRunId = run.RunId;

        await PersistExecutionStatusAsync(run, "Starting", "Preparing run");

        PruneCompletedRuns();

        run.ExecutionTask = Task.Run(() => ExecuteServiceRunAsync(run));
        await Task.Yield();
        return run.RunId;
    }

    public async Task<Guid> StartAllAsync()
    {
        lock (_sync)
        {
            if (_activeRunId is Guid activeId
                && _runs.TryGetValue(activeId, out var active)
                && !active.Completed)
                return activeId;
        }

        var run = new RunState
        {
            RunId = Guid.NewGuid(),
            Scope = "All",
            StartedAt = DateTimeOffset.UtcNow
        };

        _runs[run.RunId] = run;
        lock (_sync)
            _activeRunId = run.RunId;

        await PersistExecutionStatusAsync(run, "Starting", "Preparing run");

        PruneCompletedRuns();

        run.ExecutionTask = Task.Run(() => ExecuteAllRunAsync(run));
        await Task.Yield();
        return run.RunId;
    }

    public ApiHealthActiveRunInfo? GetActiveRun()
    {
        lock (_sync)
        {
            if (_activeRunId is not Guid activeId)
                return null;

            if (!_runs.TryGetValue(activeId, out var run) || run.Completed)
                return null;

            return new ApiHealthActiveRunInfo
            {
                RunId = run.RunId,
                Scope = run.Scope,
                ServiceName = run.ServiceName,
                StartedAt = run.StartedAt,
                IsAttachable = true
            };
        }
    }

    public async Task<ApiHealthActiveRunInfo?> GetActiveRunAsync(CancellationToken ct = default)
    {
        var local = GetActiveRun();
        if (local != null)
        {
            var localStatus = await store.GetExecutionRunStatusAsync(local.RunId, ct);
            if (localStatus == null)
                return local;

            return new ApiHealthActiveRunInfo
            {
                RunId = localStatus.RunId,
                Scope = localStatus.Scope,
                ServiceName = localStatus.ServiceName,
                StartedAt = localStatus.StartedAt,
                Phase = localStatus.Phase,
                Message = localStatus.Message,
                IsError = localStatus.IsError,
                IsAttachable = true
            };
        }

        var activeStatus = await store.GetActiveExecutionRunStatusAsync(ct);
        if (activeStatus == null)
            return null;

        return new ApiHealthActiveRunInfo
        {
            RunId = activeStatus.RunId,
            Scope = activeStatus.Scope,
            ServiceName = activeStatus.ServiceName,
            StartedAt = activeStatus.StartedAt,
            Phase = activeStatus.Phase,
            Message = activeStatus.Message,
            IsError = activeStatus.IsError,
            IsAttachable = TryGetRun(activeStatus.RunId, out _)
        };
    }

    public bool TryGetRun(Guid runId, out ApiHealthRunInfo runInfo)
    {
        if (!_runs.TryGetValue(runId, out var run))
        {
            runInfo = default;
            return false;
        }

        runInfo = new ApiHealthRunInfo
        {
            RunId = run.RunId,
            Scope = run.Scope,
            ServiceName = run.ServiceName,
            Completed = run.Completed,
            Failed = run.Failed,
            Error = run.Error,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt
        };
        return true;
    }

    public IReadOnlyList<ApiHealthStoredEvent> GetEventsSince(Guid runId, long afterSequence)
    {
        if (!_runs.TryGetValue(runId, out var run))
            return [];

        lock (run.Sync)
        {
            return run.Events
                .Where(e => e.Sequence > afterSequence)
                .Select(e => new ApiHealthStoredEvent
                {
                    Sequence = e.Sequence,
                    EventName = e.EventName,
                    Data = e.Data
                })
                .ToList();
        }
    }

    private async Task ExecuteServiceRunAsync(RunState run)
    {
        try
        {
            var serviceName = run.ServiceName ?? string.Empty;
            await AddPhaseAsync(run, "Planning", "Building execution plan");

            var suites = registry.GetSuitesForService(serviceName);
            var requirements = suites.SelectMany(s => s.GetSeedRequirements()).Distinct().ToList();

            await AddPhaseAsync(run, "Seeding", "Preparing test-owned data");
            var seedSession = await seedOrchestrator.BeginServiceAsync(serviceName, requirements, CancellationToken.None);

            if (!seedSession.Success)
            {
                var error = seedSession.Error;
                await AddPhaseAsync(run, "Failed", error, isError: true, seedRunId: seedSession.SeedRunId, seedRunName: seedSession.SeedRunName);
                await CompleteAsync(run, failed: true, error);
                AddDone(run);
                return;
            }

            await AddPhaseAsync(run, "Seeding", "Seed ready", seedRunId: seedSession.SeedRunId, seedRunName: seedSession.SeedRunName);

            await AddPhaseAsync(run, "Testing", "Executing tests");
            try
            {
                foreach (var suite in suites)
                {
                    seedContext.Current = seedSession;
                    await RunSuiteAsync(run, suite);
                }
            }
            finally
            {
                await AddPhaseAsync(run, "Cleanup", "Cleaning up seed data");
                await seedOrchestrator.EndAsync(seedSession, CancellationToken.None);
            }

            await AddPhaseAsync(run, "Done", "Service run complete");
            await CompleteAsync(run, failed: false, null);
            AddDone(run);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API Health service run failed unexpectedly. RunId={RunId}", run.RunId);
            await AddPhaseAsync(run, "Failed", SanitizedInternalError, isError: true);
            await CompleteAsync(run, failed: true, SanitizedInternalError);
            AddDone(run);
        }
    }

    private async Task ExecuteAllRunAsync(RunState run)
    {
        try
        {
            await AddPhaseAsync(run, "Planning", "Building execution plan");

            var suites = registry.GetAllSuites();
            var requirements = suites.SelectMany(s => s.GetSeedRequirements()).Distinct().ToList();

            await AddPhaseAsync(run, "Seeding", "Preparing test-owned data");
            var seedSession = await seedOrchestrator.BeginAllAsync(requirements, CancellationToken.None);

            if (!seedSession.Success)
            {
                var error = seedSession.Error;
                await AddPhaseAsync(run, "Failed", error, isError: true, seedRunId: seedSession.SeedRunId, seedRunName: seedSession.SeedRunName);
                await CompleteAsync(run, failed: true, error);
                AddDone(run);
                return;
            }

            await AddPhaseAsync(run, "Seeding", "Seed ready", seedRunId: seedSession.SeedRunId, seedRunName: seedSession.SeedRunName);

            await AddPhaseAsync(run, "Testing", "Executing tests");
            try
            {
                foreach (var suite in suites)
                {
                    seedContext.Current = seedSession;
                    await RunSuiteAsync(run, suite);
                }
            }
            finally
            {
                await AddPhaseAsync(run, "Cleanup", "Cleaning up seed data");
                await seedOrchestrator.EndAsync(seedSession, CancellationToken.None);
            }

            await AddPhaseAsync(run, "Done", "Run all complete");
            await CompleteAsync(run, failed: false, null);
            AddDone(run);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "API Health run-all failed unexpectedly. RunId={RunId}", run.RunId);
            await AddPhaseAsync(run, "Failed", SanitizedInternalError, isError: true);
            await CompleteAsync(run, failed: true, SanitizedInternalError);
            AddDone(run);
        }
    }

    private async Task RunSuiteAsync(RunState run, IServiceTestSuite suite)
    {
        IReadOnlyList<ApiTestRunResult> results;
        try
        {
            results = await suite.ExecuteAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test suite {ServiceName} failed unexpectedly.", suite.ServiceName);
            results =
            [
                new ApiTestRunResult
                {
                    EndpointKey = $"{suite.ServiceName}::SuiteError",
                    ServiceName = suite.ServiceName,
                    EndpointName = "Suite Error",
                    Passed = false,
                    ErrorMessage = SanitizedInternalError,
                    ExecutedAt = DateTimeOffset.UtcNow
                }
            ];
        }

        await store.SaveRunResultsAsync(results, CancellationToken.None);

        var informationalKeys = suite.GetEndpointDefinitions()
            .Where(d => d.IsInformational)
            .Select(d => d.Key)
            .ToHashSet();

        foreach (var result in results.Where(r => !informationalKeys.Contains(r.EndpointKey)))
        {
            var json = JsonSerializer.Serialize(result, _jsonOptions);
            AddEvent(run, "result", json);
        }
    }

    private Task AddPhaseAsync(RunState run, string phase, string message, bool isError = false, Guid? seedRunId = null, string? seedRunName = null)
    {
        var payload = JsonSerializer.Serialize(new
        {
            phase,
            scope = run.Scope,
            serviceName = run.ServiceName,
            message,
            isError,
            seedRunId,
            seedRunName,
            at = DateTimeOffset.UtcNow
        }, _jsonOptions);

        AddEvent(run, "phase", payload);
        return PersistExecutionStatusAsync(run, phase, message, isError);
    }

    private void AddDone(RunState run) => AddEvent(run, "done", "{}");

    private void AddEvent(RunState run, string eventName, string data)
    {
        lock (run.Sync)
        {
            run.Sequence++;
            run.Events.Add(new StoredEvent
            {
                Sequence = run.Sequence,
                EventName = eventName,
                Data = data
            });

            if (run.Events.Count > 5000)
                run.Events.RemoveAt(0);
        }
    }

    private async Task CompleteAsync(RunState run, bool failed, string? error)
    {
        run.Completed = true;
        run.Failed = failed;
        run.Error = error;
        run.FinishedAt = DateTimeOffset.UtcNow;

        lock (_sync)
        {
            if (_activeRunId == run.RunId)
                _activeRunId = null;
        }

        try
        {
            await store.CompleteExecutionRunAsync(run.RunId, failed, error, run.FinishedAt.Value);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist completion status for API Health run {RunId}.", run.RunId);
        }

        PruneCompletedRuns();
    }

    private async Task PersistExecutionStatusAsync(RunState run, string phase, string message, bool isError = false)
    {
        try
        {
            await store.UpsertExecutionRunStatusAsync(new ApiHealthExecutionRunStatus
            {
                RunId = run.RunId,
                Scope = run.Scope,
                ServiceName = run.ServiceName,
                StartedAt = run.StartedAt,
                Phase = phase,
                Message = message,
                IsError = isError,
                IsCompleted = run.Completed,
                Failed = run.Failed,
                Error = run.Error,
                FinishedAt = run.FinishedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist API Health run status for run {RunId}.", run.RunId);
        }
    }

    private void PruneCompletedRuns()
    {
        Guid? activeRunId;
        lock (_sync)
            activeRunId = _activeRunId;

        var now = DateTimeOffset.UtcNow;
        var completedRuns = _runs
            .Where(kvp => kvp.Value.Completed)
            .Select(kvp => new
            {
                RunId = kvp.Key,
                FinishedAt = kvp.Value.FinishedAt ?? DateTimeOffset.MinValue
            })
            .OrderBy(x => x.FinishedAt)
            .ToList();

        if (completedRuns.Count == 0)
            return;

        var runsToRemove = new HashSet<Guid>();

        foreach (var run in completedRuns)
        {
            if (run.RunId == activeRunId)
                continue;

            if (now - run.FinishedAt > CompletedRunRetention)
                runsToRemove.Add(run.RunId);
        }

        var excessCount = completedRuns.Count - MaxCompletedRunsToRetain;
        if (excessCount > 0)
        {
            foreach (var run in completedRuns.Take(excessCount))
            {
                if (run.RunId == activeRunId)
                    continue;

                runsToRemove.Add(run.RunId);
            }
        }

        foreach (var runId in runsToRemove)
            _runs.TryRemove(runId, out _);
    }

    private sealed class RunState
    {
        public Guid RunId { get; init; }
        public string Scope { get; init; } = string.Empty;
        public string? ServiceName { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset? FinishedAt { get; set; }
        public bool Completed { get; set; }
        public bool Failed { get; set; }
        public string? Error { get; set; }
        public long Sequence { get; set; }
        public object Sync { get; } = new();
        public List<StoredEvent> Events { get; } = [];
        public Task ExecutionTask { get; set; } = Task.CompletedTask;
    }

    private sealed class StoredEvent
    {
        public long Sequence { get; init; }
        public string EventName { get; init; } = string.Empty;
        public string Data { get; init; } = "{}";
    }
}

public sealed class ApiHealthStoredEvent
{
    public long Sequence { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string Data { get; init; } = "{}";
}

public sealed class ApiHealthActiveRunInfo
{
    public Guid RunId { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string? ServiceName { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public string? Phase { get; init; }
    public string? Message { get; init; }
    public bool IsError { get; init; }
    public bool IsAttachable { get; init; }
}

public sealed class ApiHealthRunInfo
{
    public Guid RunId { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string? ServiceName { get; init; }
    public bool Completed { get; init; }
    public bool Failed { get; init; }
    public string? Error { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
}
