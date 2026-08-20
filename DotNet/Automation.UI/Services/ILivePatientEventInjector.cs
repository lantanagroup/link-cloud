using Automation.UI.Models;

namespace Automation.UI.Services;

public interface ILiveCensusPublisher
{
    Task PublishAsync(PatientEventType eventType, string patientId, CancellationToken cancellationToken);
}

public sealed record LiveProvisionedPatient(string PatientId, bool ExpectedInReport);

public interface ILivePatientProvisioner
{
    Task<LiveProvisionedPatient> GenerateQualifyingPatientAsync(CancellationToken cancellationToken);
    Task<LiveProvisionedPatient> UploadPatientAsync(string content, string? fileName, CancellationToken cancellationToken);
    Task<LiveProvisionedPatient> ReferencePatientAsync(string patientId, CancellationToken cancellationToken);
}

public interface ILivePatientEventInjector
{
    LiveExpectedStateTracker OpenSession(
        Guid runId,
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        IEnumerable<string>? generatedPatientIds = null,
        ILiveCensusPublisher? censusPublisher = null,
        IEnumerable<LivePatientSeed>? poolSeeds = null,
        ILivePatientProvisioner? patientProvisioner = null);

    bool TryGetSession(Guid runId, out LiveExpectedStateTracker tracker);

    Task<IReadOnlyList<PatientStateEvent>> ApplyAutomaticAdmitsAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientStateEvent>> ApplyAutomaticDischargesAsync(
        Guid runId,
        CancellationToken cancellationToken = default);

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

    Task<LivePatientPoolEntry> GeneratePoolPatientAsync(
        Guid runId,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task<LivePatientPoolEntry> UploadPoolPatientAsync(
        Guid runId,
        string content,
        string? fileName = null,
        string? source = null,
        CancellationToken cancellationToken = default);

    Task<LivePatientPoolEntry> ReferencePoolPatientAsync(
        Guid runId,
        string patientId,
        string? source = null,
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
        IEnumerable<string>? expectedPopulation = null,
        CancellationToken cancellationToken = default);

    void CloseSession(Guid runId);
}
