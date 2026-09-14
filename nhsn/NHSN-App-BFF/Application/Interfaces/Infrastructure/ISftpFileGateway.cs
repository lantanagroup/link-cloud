using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Cerner's real sFTP connection test — Data Acquisition tests the facility's already-saved
// configuration and credentials; it does not accept ad-hoc connection details and returns no file
// listing. The caller is responsible for saving the configuration first.
public interface ISftpFileGateway
{
    Task<ConnectionResult> TestConnectionAsync(string facilityId, CancellationToken cancellationToken = default);
}
