using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using Microsoft.AspNetCore.Http;

namespace Automation.UI.Services;

/// <summary>
/// In-memory census / observability state for a live simulation window.
/// Report inclusion is Admit (auto or UI) AND the predictor flag
/// (<c>ExpectedInReport</c> from cohort/pattern/measure eligibility). Adding a patient
/// to the pool does not put them on the expected list. Discharge after Admit does not
/// remove them. Automatic census uses
/// <see cref="ScheduledInpatientPatternExtensions.GetCensusBehavior"/>.
/// Auto-discharges fire at the window midpoint (windowStart + duration/2) so they complete before freeze.
/// </summary>
public sealed class LiveExpectedStateTracker
{
    public const string AutomaticEventSource = LiveEventSources.Pattern;

    private readonly object _sync = new();
    private readonly HashSet<string> _currentlyAdmitted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dischargedDuringWindow = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expectedInReport = new(StringComparer.Ordinal);
    private readonly List<PatientStateEvent> _eventLog = [];
    private readonly Dictionary<string, MutablePoolEntry> _pool = new(StringComparer.Ordinal);
    private bool _acceptingInjections = true;
    private bool _automaticAdmitsApplied;
    private bool _automaticDischargesApplied;

    public LiveExpectedStateTracker(
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<string>? generatedPatientIds = null)
        : this(runId, windowStartUtc, windowEndUtc, ToSeeds(generatedPatientIds))
    {
    }

    public LiveExpectedStateTracker(
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<LivePatientSeed>? seeds)
    {
        RunId = runId;
        WindowStartUtc = windowStartUtc;
        WindowEndUtc = windowEndUtc;
        AutomaticDischargeAtUtc = ComputeAutomaticDischargeAtUtc(windowStartUtc, windowEndUtc);

        var distinctSeeds = (seeds ?? [])
            .Where(s => s != null && !string.IsNullOrWhiteSpace(s.PatientId))
            .Select(s => new LivePatientSeed
            {
                PatientId = s.PatientId.Trim(),
                Origin = s.Origin,
                Pattern = s.Pattern,
                ExpectedInReport = s.ExpectedInReport
            })
            .DistinctBy(s => s.PatientId, StringComparer.Ordinal)
            .ToList();

        foreach (var seed in distinctSeeds)
        {
            var expected = ResolveExpectedInReport(seed.ExpectedInReport, seed.Pattern);
            _pool[seed.PatientId] = new MutablePoolEntry
            {
                PatientId = seed.PatientId,
                Origin = seed.Origin,
                Pattern = seed.Pattern,
                CensusState = LivePatientCensusState.NotAdmitted,
                ExpectedInReport = expected
            };
        }
    }

    public Guid RunId { get; }
    public DateTimeOffset WindowStartUtc { get; }
    public DateTimeOffset WindowEndUtc { get; }
    public DateTimeOffset AutomaticDischargeAtUtc { get; }
    public DateTimeOffset? ReportGenerationTimeUtc { get; private set; }

    public bool AcceptingInjections
    {
        get { lock (_sync) return _acceptingInjections; }
    }

    public static DateTimeOffset ComputeAutomaticDischargeAtUtc(DateTimeOffset windowStartUtc, DateTimeOffset windowEndUtc)
    {
        var duration = windowEndUtc - windowStartUtc;
        if (duration <= TimeSpan.Zero)
            return windowStartUtc;

        return windowStartUtc + TimeSpan.FromTicks(duration.Ticks / 2);
    }

    public IReadOnlyList<PatientStateEvent> ApplyAutomaticAdmits(DateTimeOffset? timestampUtc = null)
    {
        lock (_sync)
        {
            EnsureAccepting();
            if (_automaticAdmitsApplied)
                return [];

            var events = new List<PatientStateEvent>();
            foreach (var entry in _pool.Values.OrderBy(e => e.PatientId, StringComparer.Ordinal))
            {
                if (entry.CensusState != LivePatientCensusState.NotAdmitted)
                    continue;
                if (!ShouldAutoAdmit(entry))
                    continue;

                var notes = entry.Origin == LivePatientOrigin.Import
                    ? "Automatic admit of scenario-loaded imported patient expected in the report"
                    : "Automatic admit from inpatient pattern";
                events.Add(AdmitUnlocked(
                    entry.PatientId,
                    AutomaticEventSource,
                    notes,
                    timestampUtc ?? WindowStartUtc));
            }

            _automaticAdmitsApplied = true;
            return events;
        }
    }

    public IReadOnlyList<PatientStateEvent> ApplyAutomaticDischarges(DateTimeOffset? timestampUtc = null)
    {
        lock (_sync)
        {
            EnsureAccepting();
            if (_automaticDischargesApplied)
                return [];

            var events = new List<PatientStateEvent>();
            foreach (var entry in _pool.Values.OrderBy(e => e.PatientId, StringComparer.Ordinal))
            {
                if (entry.Pattern is not { } pattern)
                    continue;
                if (!pattern.GetCensusBehavior().EmitDischargeDuringWindow)
                    continue;
                if (entry.CensusState != LivePatientCensusState.Admitted)
                    continue;

                events.Add(DischargeUnlocked(
                    entry.PatientId,
                    AutomaticEventSource,
                    "Automatic discharge from inpatient pattern",
                    timestampUtc ?? AutomaticDischargeAtUtc));
            }

            _automaticDischargesApplied = true;
            return events;
        }
    }

    public LivePatientPoolEntry AddToPool(
        string patientId,
        LivePatientOrigin origin,
        ScheduledInpatientPattern? pattern = null,
        bool? expectedInReport = null,
        string? source = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new LiveInjectionException("patientId is required.", StatusCodes.Status400BadRequest);

        lock (_sync)
        {
            EnsureAccepting();
            var id = patientId.Trim();
            if (_pool.ContainsKey(id))
                throw new LiveInjectionException($"Patient '{id}' is already in the live pool.", StatusCodes.Status409Conflict);

            var expected = ResolveExpectedInReport(expectedInReport, pattern);
            _pool[id] = new MutablePoolEntry
            {
                PatientId = id,
                Origin = origin,
                Pattern = pattern,
                CensusState = LivePatientCensusState.NotAdmitted,
                ExpectedInReport = expected
            };
            _eventLog.Add(NewEvent(
                id,
                PatientEventType.Inject,
                source ?? origin.ToString(),
                notes ?? $"Added to pool via {origin}",
                timestampUtc: null));
            return _pool[id].ToEntry();
        }
    }

    public PatientStateEvent Admit(string? patientId, string? source, string? notes, DateTimeOffset? timestampUtc = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new LiveInjectionException("patientId is required for admit.", StatusCodes.Status400BadRequest);

        lock (_sync)
        {
            EnsureAccepting();
            return AdmitUnlocked(patientId.Trim(), source, notes, timestampUtc);
        }
    }

    public PatientStateEvent Discharge(string patientId, string? source, string? notes, DateTimeOffset? timestampUtc = null)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new LiveInjectionException("patientId is required for discharge.", StatusCodes.Status400BadRequest);

        lock (_sync)
        {
            EnsureAccepting();
            return DischargeUnlocked(patientId.Trim(), source, notes, timestampUtc);
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
            var pool = SnapshotPoolUnlocked();
            return new LivePatientStateSnapshot
            {
                Admitted = _currentlyAdmitted.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                DischargedDuringWindow = _dischargedDuringWindow.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                ExpectedPopulation = SnapshotExpectedUnlocked(),
                Pool = pool,
                PoolTotals = ComputeTotals(pool),
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
            return SnapshotExpectedUnlocked();
    }

    public LiveSimulationDiagnostics ToDiagnostics(
        IEnumerable<string>? actualPopulation = null,
        bool? inclusionPassed = null,
        IEnumerable<string>? missing = null,
        IEnumerable<string>? unexpected = null,
        IEnumerable<string>? expectedPopulation = null)
    {
        lock (_sync)
        {
            var pool = SnapshotPoolUnlocked();
            var expected = expectedPopulation != null
                ? expectedPopulation
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList()
                : SnapshotExpectedUnlocked().ToList();
            return new LiveSimulationDiagnostics
            {
                WindowStartUtc = WindowStartUtc,
                WindowEndUtc = WindowEndUtc,
                ReportGenerationTimeUtc = ReportGenerationTimeUtc,
                EventLog = [.. _eventLog],
                CurrentlyAdmitted = _currentlyAdmitted.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                DischargedDuringWindow = _dischargedDuringWindow.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                ExpectedPopulation = expected,
                Pool = pool.ToList(),
                PoolTotals = ComputeTotals(pool),
                ActualPopulation = actualPopulation?.OrderBy(id => id, StringComparer.Ordinal).ToList() ?? [],
                InclusionPassed = inclusionPassed,
                MissingFromReport = missing?.OrderBy(id => id, StringComparer.Ordinal).ToList() ?? [],
                UnexpectedInReport = unexpected?.OrderBy(id => id, StringComparer.Ordinal).ToList() ?? []
            };
        }
    }

    private PatientStateEvent AdmitUnlocked(string id, string? source, string? notes, DateTimeOffset? timestampUtc)
    {
        if (_currentlyAdmitted.Contains(id))
            throw new LiveInjectionException($"Patient '{id}' is already admitted.", StatusCodes.Status409Conflict);

        if (_pool.Count > 0 && !_pool.ContainsKey(id))
            throw new LiveInjectionException($"Patient '{id}' is not in the live pool.", StatusCodes.Status409Conflict);

        _currentlyAdmitted.Add(id);
        _dischargedDuringWindow.Remove(id);
        UpdatePoolState(id, LivePatientCensusState.Admitted);
        if (_pool.TryGetValue(id, out var admittedEntry) && admittedEntry.ExpectedInReport)
            _expectedInReport.Add(id);

        var evt = NewEvent(id, PatientEventType.Admit, source, notes, timestampUtc);
        _eventLog.Add(evt);
        return evt;
    }

    private PatientStateEvent DischargeUnlocked(string id, string? source, string? notes, DateTimeOffset? timestampUtc)
    {
        if (_pool.Count > 0 && !_pool.ContainsKey(id))
            throw new LiveInjectionException($"Patient '{id}' is not in the live pool.", StatusCodes.Status409Conflict);

        if (!_currentlyAdmitted.Remove(id))
            throw new LiveInjectionException($"Patient '{id}' is not currently admitted.", StatusCodes.Status409Conflict);

        _dischargedDuringWindow.Add(id);
        UpdatePoolState(id, LivePatientCensusState.DischargedDuringWindow);

        var evt = NewEvent(id, PatientEventType.Discharge, source, notes, timestampUtc);
        _eventLog.Add(evt);
        return evt;
    }

    private void UpdatePoolState(string id, LivePatientCensusState state)
    {
        if (_pool.TryGetValue(id, out var entry))
        {
            entry.CensusState = state;
            return;
        }

        _pool[id] = new MutablePoolEntry
        {
            PatientId = id,
            Origin = LivePatientOrigin.Generated,
            CensusState = state,
            ExpectedInReport = _expectedInReport.Contains(id)
        };
    }

    private string[] SnapshotExpectedUnlocked()
        => _expectedInReport.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    private static bool ShouldAutoAdmit(MutablePoolEntry entry)
    {
        if (entry.Pattern is { } pattern)
            return pattern.GetCensusBehavior().EmitAdmitDuringWindow;

        // No generated inpatient pattern means this is a scenario-loaded import
        // (or an unpatterned seed). Admit at window open so the run is hands-off.
        // Report inclusion is still Admit AND ExpectedInReport (see AdmitUnlocked).
        return true;
    }

    private static bool ResolveExpectedInReport(bool? expectedInReport, ScheduledInpatientPattern? pattern)
    {
        if (expectedInReport.HasValue)
            return expectedInReport.Value;
        return pattern?.GetCensusBehavior().ExpectedInReport ?? false;
    }

    private IReadOnlyList<LivePatientPoolEntry> SnapshotPoolUnlocked()
        => _pool.Values
            .OrderBy(e => e.PatientId, StringComparer.Ordinal)
            .Select(e => e.ToEntry())
            .ToArray();

    private static LivePatientPoolTotals ComputeTotals(IReadOnlyList<LivePatientPoolEntry> pool)
        => new()
        {
            Total = pool.Count,
            NotAdmitted = pool.Count(p => p.CensusState == LivePatientCensusState.NotAdmitted),
            Admitted = pool.Count(p => p.CensusState == LivePatientCensusState.Admitted),
            DischargedDuringWindow = pool.Count(p => p.CensusState == LivePatientCensusState.DischargedDuringWindow)
        };

    private void EnsureAccepting()
    {
        if (!_acceptingInjections)
            throw new LiveInjectionException("Live window is not accepting injections.", StatusCodes.Status409Conflict);
    }

    private static IEnumerable<LivePatientSeed> ToSeeds(IEnumerable<string>? generatedPatientIds)
        => (generatedPatientIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => new LivePatientSeed
            {
                PatientId = id.Trim(),
                Origin = LivePatientOrigin.Cohort
            });

    private sealed class MutablePoolEntry
    {
        public string PatientId { get; init; } = "";
        public LivePatientOrigin Origin { get; init; }
        public ScheduledInpatientPattern? Pattern { get; init; }
        public LivePatientCensusState CensusState { get; set; }
        public bool ExpectedInReport { get; init; }

        public LivePatientPoolEntry ToEntry()
            => new()
            {
                PatientId = PatientId,
                Origin = Origin,
                Pattern = Pattern,
                CensusState = CensusState,
                ExpectedInReport = ExpectedInReport
            };
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
