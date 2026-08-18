using Automation.UI.Models;
using Microsoft.AspNetCore.Http;

namespace Automation.UI.Services;

/// <summary>
/// In-memory ExpectedState for a live simulation window.
/// Final expected set = DischargedDuringWindow ∪ CurrentlyAdmitted.
/// </summary>
public sealed class LiveExpectedStateTracker
{
    private readonly object _sync = new();
    private readonly HashSet<string> _currentlyAdmitted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dischargedDuringWindow = new(StringComparer.Ordinal);
    private readonly List<PatientStateEvent> _eventLog = [];
    private readonly Queue<string> _availablePatientIds;
    private bool _acceptingInjections = true;

    public LiveExpectedStateTracker(
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<string>? generatedPatientIds = null)
    {
        RunId = runId;
        WindowStartUtc = windowStartUtc;
        WindowEndUtc = windowEndUtc;
        _availablePatientIds = new Queue<string>(
            (generatedPatientIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal));
    }

    public Guid RunId { get; }
    public DateTimeOffset WindowStartUtc { get; }
    public DateTimeOffset WindowEndUtc { get; }
    public DateTimeOffset? ReportGenerationTimeUtc { get; private set; }

    public bool AcceptingInjections
    {
        get { lock (_sync) return _acceptingInjections; }
    }

    public PatientStateEvent Admit(string? patientId, string? source, string? notes, DateTimeOffset? timestampUtc = null)
    {
        lock (_sync)
        {
            EnsureAccepting();
            var id = ResolveAdmitPatientId(patientId);
            _currentlyAdmitted.Add(id);

            var evt = NewEvent(id, PatientEventType.Admit, source, notes, timestampUtc);
            _eventLog.Add(evt);
            return evt;
        }
    }

    public PatientStateEvent Discharge(string patientId, string? source, string? notes, DateTimeOffset? timestampUtc = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new LiveInjectionException("patientId is required for discharge.", StatusCodes.Status400BadRequest);

        lock (_sync)
        {
            EnsureAccepting();
            var id = patientId.Trim();
            if (!_currentlyAdmitted.Remove(id))
                throw new LiveInjectionException($"Patient '{id}' is not currently admitted.", StatusCodes.Status409Conflict);

            _dischargedDuringWindow.Add(id);
            var evt = NewEvent(id, PatientEventType.Discharge, source, notes, timestampUtc);
            _eventLog.Add(evt);
            return evt;
        }
    }

    public void Freeze(DateTimeOffset? reportGenerationTimeUtc = null)
    {
        lock (_sync)
        {
            _acceptingInjections = false;
            ReportGenerationTimeUtc = reportGenerationTimeUtc ?? DateTimeOffset.UtcNow;
        }
    }

    public LivePatientStateSnapshot GetState()
    {
        lock (_sync)
        {
            return new LivePatientStateSnapshot
            {
                Admitted = _currentlyAdmitted.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                DischargedDuringWindow = _dischargedDuringWindow.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                ExpectedPopulation = ComputeExpectedUnlocked().OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                AcceptingInjections = _acceptingInjections,
                WindowStartUtc = WindowStartUtc,
                WindowEndUtc = WindowEndUtc,
                ReportGenerationTimeUtc = ReportGenerationTimeUtc
            };
        }
    }

    public IReadOnlyList<PatientStateEvent> GetEvents()
    {
        lock (_sync)
            return _eventLog.ToArray();
    }

    public IReadOnlyList<string> GetExpectedPopulation()
    {
        lock (_sync)
            return ComputeExpectedUnlocked().OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    public LiveSimulationDiagnostics ToDiagnostics(
        IEnumerable<string>? actualPopulation = null,
        bool? inclusionPassed = null,
        IEnumerable<string>? missing = null,
        IEnumerable<string>? unexpected = null)
    {
        lock (_sync)
        {
            return new LiveSimulationDiagnostics
            {
                WindowStartUtc = WindowStartUtc,
                WindowEndUtc = WindowEndUtc,
                ReportGenerationTimeUtc = ReportGenerationTimeUtc,
                EventLog = [.. _eventLog],
                CurrentlyAdmitted = _currentlyAdmitted.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                DischargedDuringWindow = _dischargedDuringWindow.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                ExpectedPopulation = ComputeExpectedUnlocked().OrderBy(id => id, StringComparer.Ordinal).ToList(),
                ActualPopulation = actualPopulation?.OrderBy(id => id, StringComparer.Ordinal).ToList() ?? [],
                InclusionPassed = inclusionPassed,
                MissingFromReport = missing?.OrderBy(id => id, StringComparer.Ordinal).ToList() ?? [],
                UnexpectedInReport = unexpected?.OrderBy(id => id, StringComparer.Ordinal).ToList() ?? []
            };
        }
    }

    private HashSet<string> ComputeExpectedUnlocked()
        => new(_dischargedDuringWindow.Concat(_currentlyAdmitted), StringComparer.Ordinal);

    private void EnsureAccepting()
    {
        if (!_acceptingInjections)
            throw new LiveInjectionException("Live window is not accepting injections.", StatusCodes.Status409Conflict);
    }

    private string ResolveAdmitPatientId(string? patientId)
    {
        if (!string.IsNullOrWhiteSpace(patientId))
            return patientId.Trim();

        while (_availablePatientIds.Count > 0)
        {
            var candidate = _availablePatientIds.Dequeue();
            if (!_currentlyAdmitted.Contains(candidate))
                return candidate;
        }

        return $"live-{Guid.NewGuid():N}";
    }

    private PatientStateEvent NewEvent(
        string patientId,
        PatientEventType eventType,
        string? source,
        string? notes,
        DateTimeOffset? timestampUtc)
        => new()
        {
            EventId = Guid.NewGuid(),
            RunId = RunId,
            PatientId = patientId,
            EventType = eventType,
            TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow,
            Source = source,
            Notes = notes
        };
}

public sealed class LiveInjectionException : Exception
{
    public LiveInjectionException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
