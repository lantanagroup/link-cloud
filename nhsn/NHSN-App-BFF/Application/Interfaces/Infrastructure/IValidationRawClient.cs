namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Validation's per-patient result endpoint doesn't fit LinkSdk's IValidationServiceClient shape
// (that client only covers the report-level result read) -- see ValidationRawClient. LinkSdk is
// owned by another team, so a missing method there is a cross-team blocker, not something to add
// here ourselves.
public interface IValidationRawClient
{
    Task<string> GetPatientResultsAsync(string facilityId, string reportId, string patientId, string severity, CancellationToken cancellationToken = default);
}
