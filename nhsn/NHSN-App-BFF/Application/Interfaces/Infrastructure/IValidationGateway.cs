using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

public interface IValidationGateway
{
    Task<IReadOnlyList<PreQualIssue>> GetPatientResultsAsync(string facilityId, string reportId, string patientId, CancellationToken cancellationToken = default);
}
