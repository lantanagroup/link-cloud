using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IAcknowledgementService
{
    // The most recently recorded Accepted value for a facility, Kind and context, or null when
    // none has been recorded yet.
    Task<bool?> GetLatestAsync(string facilityId, AcknowledgementKind kind, string? contextId = null, CancellationToken cancellationToken = default);

    // Appends a new attestation row. Never updates an existing one.
    Task RecordAsync(
        string facilityId,
        AcknowledgementKind kind,
        string? contextId,
        bool accepted,
        string statementKey,
        string acceptedByExternalUserId,
        CancellationToken cancellationToken = default);
}
