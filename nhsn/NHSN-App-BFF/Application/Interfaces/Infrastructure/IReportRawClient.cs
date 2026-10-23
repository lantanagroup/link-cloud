namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Report's per-patient entry detail carries AggregateReportUri/MeasureReportUri -- the blob
// storage locations of the report Report already generated -- but LinkSdk's typed
// IReportServiceClient deserializes that route into ReportEntryDetailApiModel, which declares
// neither property, so they're silently dropped. LinkSdk is owned by another team, so a missing
// field there is a cross-team blocker, not something to add here ourselves. Mirrors
// ValidationRawClient/NormalizationRawClient.
public interface IReportRawClient
{
    /// <summary>
    /// Raw JSON for Report's per-patient entry detail, or null when Report has no entry for this
    /// schedule/patient pair yet -- unlike Validation's raw client, a 404 here is a normal,
    /// expected state (not yet reported) rather than an error.
    /// </summary>
    Task<string?> GetEntryDetailRawAsync(string reportScheduleId, string patientId, CancellationToken cancellationToken = default);
}
