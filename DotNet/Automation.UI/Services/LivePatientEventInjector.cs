using System.Collections.Concurrent;
using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Models;
using Microsoft.AspNetCore.SignalR;

namespace Automation.UI.Services;

public sealed class LivePatientEventInjector(
    ISnapshotStore snapshotStore,
    IHubContext<RunHub> hub,
    ILogger<LivePatientEventInjector> logger) : ILivePatientEventInjector
{
    public const string SnapshotDomain = "liveSimulation";

    private readonly ConcurrentDictionary<Guid, LiveSession> _sessions = new();

    public LiveExpectedStateTracker OpenSession(
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<string>? generatedPatientIds = null,
        ILiveCensusPublisher? censusPublisher = null)
    {
        var tracker = new LiveExpectedStateTracker(runId, windowStartUtc, windowEndUtc, generatedPatientIds);
        var session = new LiveSession(tracker, censusPublisher)
        {
            LastDiagnostics = tracker.ToDiagnostics()
        };
        _sessions[runId] = session;
        _ = PersistAsync(session, CancellationToken.None);
        return tracker;
    }

    public bool TryGetSession(Guid runId, out LiveExpectedStateTracker tracker)
    {
        if (_sessions.TryGetValue(runId, out var session))
        {
            tracker = session.Tracker;
            return true;
        }

        tracker = null!;
        return false;
    }

    public async Task<PatientStateEvent> AdmitAsync(
        Guid runId,
        string? patientId,
        string? source,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var session = GetRequiredSession(runId);
        PatientStateEvent evt;
        try
        {
            evt = session.Tracker.Admit(patientId, source, notes);
        }
        catch (LiveInjectionException)
        {
            throw;
        }

        try
        {
            if (session.CensusPublisher != null)
                await session.CensusPublisher.PublishAsync(PatientEventType.Admit, evt.PatientId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish live admit for run {RunId}, patient {PatientId}.", runId, evt.PatientId);
            throw new LiveInjectionException(
                $"Admit recorded locally but census publish failed: {ex.Message}",
                StatusCodes.Status502BadGateway);
        }
        finally
        {
            await PersistAndBroadcastAsync(session, evt, cancellationToken);
        }

        return evt;
    }

    public async Task<PatientStateEvent> DischargeAsync(
        Guid runId,
        string patientId,
        string? source,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var session = GetRequiredSession(runId);
        var evt = session.Tracker.Discharge(patientId, source, notes);

        try
        {
            if (session.CensusPublisher != null)
                await session.CensusPublisher.PublishAsync(PatientEventType.Discharge, evt.PatientId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish live discharge for run {RunId}, patient {PatientId}.", runId, evt.PatientId);
            throw new LiveInjectionException(
                $"Discharge recorded locally but census publish failed: {ex.Message}",
                StatusCodes.Status502BadGateway);
        }
        finally
        {
            await PersistAndBroadcastAsync(session, evt, cancellationToken);
        }

        return evt;
    }

    public IReadOnlyList<PatientStateEvent> GetEvents(Guid runId)
    {
        return _sessions.TryGetValue(runId, out var session)
            ? session.Tracker.GetEvents()
            : [];
    }

    public async Task<IReadOnlyList<PatientStateEvent>> GetEventsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(runId, out var session))
            return session.Tracker.GetEvents();

        var snapshot = await snapshotStore.GetDomainAsync<LiveSimulationDiagnostics>(runId, SnapshotDomain, cancellationToken);
        return snapshot?.Data.EventLog ?? [];
    }

    public LivePatientStateSnapshot GetState(Guid runId)
    {
        return _sessions.TryGetValue(runId, out var session)
            ? session.Tracker.GetState()
            : new LivePatientStateSnapshot();
    }

    public async Task<LivePatientStateSnapshot> GetStateAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(runId, out var session))
            return session.Tracker.GetState();

        var snapshot = await snapshotStore.GetDomainAsync<LiveSimulationDiagnostics>(runId, SnapshotDomain, cancellationToken);
        if (snapshot?.Data == null)
            return new LivePatientStateSnapshot();

        var data = snapshot.Data;
        return new LivePatientStateSnapshot
        {
            Admitted = data.CurrentlyAdmitted,
            DischargedDuringWindow = data.DischargedDuringWindow,
            ExpectedPopulation = data.ExpectedPopulation,
            AcceptingInjections = false,
            WindowStartUtc = data.WindowStartUtc,
            WindowEndUtc = data.WindowEndUtc,
            ReportGenerationTimeUtc = data.ReportGenerationTimeUtc
        };
    }

    public LiveSimulationDiagnostics GetDiagnostics(Guid runId)
    {
        return _sessions.TryGetValue(runId, out var session)
            ? session.LastDiagnostics ?? session.Tracker.ToDiagnostics()
            : new LiveSimulationDiagnostics();
    }

    public async Task<LiveSimulationDiagnostics> GetDiagnosticsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(runId, out var session))
            return session.LastDiagnostics ?? session.Tracker.ToDiagnostics();

        var snapshot = await snapshotStore.GetDomainAsync<LiveSimulationDiagnostics>(runId, SnapshotDomain, cancellationToken);
        return snapshot?.Data ?? new LiveSimulationDiagnostics();
    }

    public async Task NotifyWindowClosingAsync(Guid runId, DateTimeOffset closeTime, CancellationToken cancellationToken = default)
    {
        await hub.Clients.Group(runId.ToString()).SendAsync("LiveWindowClosing", closeTime, cancellationToken);
    }

    public async Task FreezeAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(runId, out var session))
            return;

        session.Tracker.Freeze();
        session.LastDiagnostics = session.Tracker.ToDiagnostics();
        await PersistAsync(session, cancellationToken);
        await hub.Clients.Group(runId.ToString()).SendAsync(
            "LiveWindowClosed",
            cancellationToken);
        await hub.Clients.Group(runId.ToString()).SendAsync(
            "PatientStateChanged",
            session.Tracker.GetState(),
            cancellationToken);
    }

    public async Task RecordActualPopulationAsync(
        Guid runId,
        IEnumerable<string> actualPopulation,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(runId, out var session))
            return;

        var actual = actualPopulation
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var expected = session.Tracker.GetExpectedPopulation();
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(id => !actualSet.Contains(id)).ToArray();
        var unexpected = actual.Where(id => !expectedSet.Contains(id)).ToArray();
        var passed = missing.Length == 0;

        session.LastDiagnostics = session.Tracker.ToDiagnostics(actual, passed, missing, unexpected);
        await PersistAsync(session, cancellationToken);
    }

    public void CloseSession(Guid runId)
        => _sessions.TryRemove(runId, out _);

    private LiveSession GetRequiredSession(Guid runId)
    {
        if (_sessions.TryGetValue(runId, out var session))
            return session;

        throw new LiveInjectionException(
            "Live window is not accepting injections.",
            StatusCodes.Status409Conflict);
    }

    private async Task PersistAndBroadcastAsync(
        LiveSession session,
        PatientStateEvent evt,
        CancellationToken cancellationToken)
    {
        session.LastDiagnostics = session.Tracker.ToDiagnostics();
        await PersistAsync(session, cancellationToken);

        var group = session.Tracker.RunId.ToString();
        await hub.Clients.Group(group).SendAsync("PatientEventInjected", evt, cancellationToken);
        await hub.Clients.Group(group).SendAsync("PatientStateChanged", session.Tracker.GetState(), cancellationToken);
    }

    private async Task PersistAsync(LiveSession session, CancellationToken cancellationToken)
    {
        try
        {
            var diagnostics = session.LastDiagnostics ?? session.Tracker.ToDiagnostics();
            await snapshotStore.SetDomainAsync(session.Tracker.RunId, SnapshotDomain, diagnostics, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist live simulation snapshot for run {RunId}.", session.Tracker.RunId);
        }
    }

    private sealed class LiveSession(LiveExpectedStateTracker tracker, ILiveCensusPublisher? censusPublisher)
    {
        public LiveExpectedStateTracker Tracker { get; } = tracker;
        public ILiveCensusPublisher? CensusPublisher { get; } = censusPublisher;
        public LiveSimulationDiagnostics? LastDiagnostics { get; set; }
    }
}
