using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Cerner's sFTP file listing, for the authenticated caller's facility.
public interface IPatientsOfInterestService
{
    Task<IReadOnlyList<SftpFile>> GetSftpFilesAsync(CancellationToken cancellationToken = default);
}
