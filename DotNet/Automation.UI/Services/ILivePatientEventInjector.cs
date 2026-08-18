using Automation.UI.Models;

namespace Automation.UI.Services;

public interface ILiveCensusPublisher
{
    Task PublishAsync(PatientEventType eventType, string patientId, CancellationToken cancellationToken);
}

public interface ILivePatientEventInjector
{
    LiveExpectedStateTracker OpenSession(
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<string>? generatedPatientIds = null,
        ILiveCensusPublisher? censusPublisher = null);

    bool TryGetSession(Guid runId, out LiveExpectedStateTracker tracker);

    Task<PatientStateEvent> AdmitAsync(
        Guid runId,
        string? patientId,
        string? source,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<PatientStateEvent> DischargeAsync(
        Guid runId,
        string patientId,
        string? source,
        string? notes = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<PatientStateEvent> GetEvents(Guid runId);

    Task<IReadOnlyList<PatientStateEvent>> GetEventsAsync(Guid runId, CancellationToken cancellationToken = default);

    LivePatientStateSnapshot GetState(Guid runId);

    Task<LivePatientStateSnapshot> GetStateAsync(Guid runId, CancellationToken cancellationToken = default);

    LiveSimulationDiagnostics GetDiagnostics(Guid runId);

    Task<LiveSimulationDiagnostics> GetDiagnosticsAsync(Guid runId, CancellationToken cancellationToken = default);

    Task NotifyWindowClosingAsync(Guid runId, DateTimeOffset closeTime, CancellationToken cancellationToken = default);

    Task FreezeAsync(Guid runId, CancellationToken cancellationToken = default);

    Task RecordActualPopulationAsync(
        Guid runId,
        IEnumerable<string> actualPopulation,
        CancellationToken cancellationToken = default);

    void CloseSession(Guid runId);
}
