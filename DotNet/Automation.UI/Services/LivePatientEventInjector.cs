using System.Collections.Concurrent;
using Automation.UI.Models;
using LantanaGroup.Link.Automation.Link.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
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
        ILiveCensusPublisher? censusPublisher = null,
        IEnumerable<LivePatientSeed>? poolSeeds = null,
        ILivePatientProvisioner? patientProvisioner = null)
    {
        var tracker = poolSeeds != null
            ? new LiveExpectedStateTracker(runId, windowStartUtc, windowEndUtc, poolSeeds)
            : new LiveExpectedStateTracker(runId, windowStartUtc, windowEndUtc, generatedPatientIds);
        var session = new LiveSession(tracker, censusPublisher, patientProvisioner)
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

    public Task<IReadOnlyList<PatientStateEvent>> ApplyAutomaticAdmitsAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
        => ApplyAutomaticEventsAsync(runId, tracker => tracker.ApplyAutomaticAdmits(), cancellationToken);

    public Task<IReadOnlyList<PatientStateEvent>> ApplyAutomaticDischargesAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
        => ApplyAutomaticEventsAsync(runId, tracker => tracker.ApplyAutomaticDischarges(), cancellationToken);

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
            logger.LogError(ex, "Failed to publish live admit for run {RunId}, patient {PatientId}.", runId, ForLog(evt.PatientId));
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
            logger.LogError(ex, "Failed to publish live discharge for run {RunId}, patient {PatientId}.", runId, ForLog(evt.PatientId));
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

    public async Task<LivePatientPoolEntry> GeneratePoolPatientAsync(
        Guid runId,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var session = GetRequiredSession(runId);
        LiveProvisionedPatient provisioned;
        try
        {
            provisioned = session.PatientProvisioner != null
                ? await session.PatientProvisioner.GenerateQualifyingPatientAsync(cancellationToken)
                : new LiveProvisionedPatient($"live-gen-{Guid.NewGuid():N}", ExpectedInReport: false);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate live pool patient for run {RunId}.", runId);
            throw new LiveInjectionException(
                $"Failed to generate patient: {ex.Message}",
                StatusCodes.Status502BadGateway);
        }

        return await AddPoolPatientAsync(
            session,
            provisioned.PatientId,
            LivePatientOrigin.Generated,
            cancellationToken,
            expectedInReport: provisioned.ExpectedInReport,
            source: source ?? LiveEventSources.Generated);
    }

    public async Task<LivePatientPoolEntry> UploadPoolPatientAsync(
        Guid runId,
        string content,
        string? fileName = null,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new LiveInjectionException("Upload content is required.", StatusCodes.Status400BadRequest);

        var session = GetRequiredSession(runId);
        LiveProvisionedPatient provisioned;
        try
        {
            provisioned = session.PatientProvisioner != null
                ? await session.PatientProvisioner.UploadPatientAsync(content, fileName, cancellationToken)
                : new LiveProvisionedPatient(
                    ExtractPatientIdFromBundle(content) ?? $"live-upload-{Guid.NewGuid():N}",
                    ExpectedInReport: false);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload live pool patient for run {RunId}.", runId);
            throw new LiveInjectionException(
                $"Failed to upload patient: {ex.Message}",
                StatusCodes.Status502BadGateway);
        }

        return await AddPoolPatientAsync(
            session,
            provisioned.PatientId,
            LivePatientOrigin.Upload,
            cancellationToken,
            expectedInReport: provisioned.ExpectedInReport,
            source: source ?? LiveEventSources.Upload);
    }

    public async Task<LivePatientPoolEntry> ReferencePoolPatientAsync(
        Guid runId,
        string patientId,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new LiveInjectionException("patientId is required.", StatusCodes.Status400BadRequest);

        var session = GetRequiredSession(runId);
        LiveProvisionedPatient provisioned;
        try
        {
            provisioned = session.PatientProvisioner != null
                ? await session.PatientProvisioner.ReferencePatientAsync(patientId.Trim(), cancellationToken)
                : new LiveProvisionedPatient(patientId.Trim(), ExpectedInReport: false);
        }
        catch (LiveInjectionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reference live pool patient {PatientId} for run {RunId}.", ForLog(patientId), runId);
            throw new LiveInjectionException(
                $"Failed to reference patient: {ex.Message}",
                StatusCodes.Status502BadGateway);
        }

        return await AddPoolPatientAsync(
            session,
            provisioned.PatientId,
            LivePatientOrigin.FhirId,
            cancellationToken,
            expectedInReport: provisioned.ExpectedInReport,
            source: source ?? LiveEventSources.FhirId);
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
            Pool = data.Pool,
            PoolTotals = data.PoolTotals,
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

        if (session.Tracker.AcceptingInjections)
        {
            try
            {
                await ApplyAutomaticDischargesAsync(runId, cancellationToken);
            }
            catch (LiveInjectionException)
            {
                // Window may already be frozen or empty; freeze still proceeds.
            }
        }

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
        IEnumerable<string>? expectedPopulation = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(runId, out var session))
            return;

        var actual = actualPopulation
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        // Callers may pass a realized expected set (admitted AND predictor-qualifying).
        // When omitted, the tracker snapshot is used.
        var expected = (expectedPopulation ?? session.Tracker.GetExpectedPopulation())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        var missing = expected.Where(id => !actualSet.Contains(id)).ToArray();
        var unexpected = actual.Where(id => !expectedSet.Contains(id)).ToArray();
        var passed = missing.Length == 0;

        session.LastDiagnostics = session.Tracker.ToDiagnostics(
            actual,
            passed,
            missing,
            unexpected,
            expected);
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

    private async Task<IReadOnlyList<PatientStateEvent>> ApplyAutomaticEventsAsync(
        Guid runId,
        Func<LiveExpectedStateTracker, IReadOnlyList<PatientStateEvent>> apply,
        CancellationToken cancellationToken)
    {
        var session = GetRequiredSession(runId);
        var events = apply(session.Tracker);
        foreach (var evt in events)
        {
            try
            {
                if (session.CensusPublisher != null)
                    await session.CensusPublisher.PublishAsync(evt.EventType, evt.PatientId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish automatic {EventType} for run {RunId}, patient {PatientId}.", evt.EventType, runId, ForLog(evt.PatientId));
                throw new LiveInjectionException(
                    $"Automatic {evt.EventType} recorded locally but census publish failed: {ex.Message}",
                    StatusCodes.Status502BadGateway);
            }
            finally
            {
                await PersistAndBroadcastAsync(session, evt, cancellationToken);
            }
        }

        if (events.Count == 0)
        {
            session.LastDiagnostics = session.Tracker.ToDiagnostics();
            await PersistAsync(session, cancellationToken);
        }

        return events;
    }

    private async Task<LivePatientPoolEntry> AddPoolPatientAsync(
        LiveSession session,
        string patientId,
        LivePatientOrigin origin,
        CancellationToken cancellationToken,
        bool? expectedInReport = null,
        string? source = null)
    {
        LivePatientPoolEntry entry;
        try
        {
            entry = session.Tracker.AddToPool(
                patientId,
                origin,
                expectedInReport: expectedInReport,
                source: source);
        }
        catch (LiveInjectionException)
        {
            throw;
        }

        session.LastDiagnostics = session.Tracker.ToDiagnostics();
        await PersistAsync(session, cancellationToken);
        var group = session.Tracker.RunId.ToString();
        var injectEvent = session.Tracker.GetEvents().LastOrDefault(e =>
            e.EventType == PatientEventType.Inject
            && string.Equals(e.PatientId, entry.PatientId, StringComparison.Ordinal));
        if (injectEvent != null)
            await hub.Clients.Group(group).SendAsync("PatientEventInjected", injectEvent, cancellationToken);
        await hub.Clients.Group(group).SendAsync("PoolPatientAdded", entry, cancellationToken);
        await hub.Clients.Group(group).SendAsync("PatientStateChanged", session.Tracker.GetState(), cancellationToken);
        return entry;
    }

    private static string? ExtractPatientIdFromBundle(string content)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            var resourceType = root.TryGetProperty("resourceType", out var rt) ? rt.GetString() : null;
            if (!string.Equals(resourceType, "Bundle", StringComparison.OrdinalIgnoreCase)
                && root.TryGetProperty("id", out var rootId)
                && rootId.ValueKind == System.Text.Json.JsonValueKind.String
                && !string.IsNullOrWhiteSpace(rootId.GetString()))
            {
                return rootId.GetString();
            }

            if (root.TryGetProperty("entry", out var entries) && entries.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("resource", out var resource))
                        continue;
                    var type = resource.TryGetProperty("resourceType", out var typeEl) ? typeEl.GetString() : null;
                    if (!string.Equals(type, "Patient", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (resource.TryGetProperty("id", out var idEl)
                        && idEl.ValueKind == System.Text.Json.JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(idEl.GetString()))
                    {
                        return idEl.GetString();
                    }
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return null;
    }

    // CodeQL cs/log-forging treats Replace as a barrier; SanitizeForLog alone is not recognized.
    private static string ForLog(string? value)
        => (value ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .SanitizeForLog() ?? string.Empty;

    private sealed class LiveSession(
        LiveExpectedStateTracker tracker,
        ILiveCensusPublisher? censusPublisher,
        ILivePatientProvisioner? patientProvisioner)
    {
        public LiveExpectedStateTracker Tracker { get; } = tracker;
        public ILiveCensusPublisher? CensusPublisher { get; } = censusPublisher;
        public ILivePatientProvisioner? PatientProvisioner { get; } = patientProvisioner;
        public LiveSimulationDiagnostics? LastDiagnostics { get; set; }
    }
}
