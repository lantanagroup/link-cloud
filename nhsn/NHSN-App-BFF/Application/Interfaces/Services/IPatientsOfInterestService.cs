using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// The census step's backend, for the authenticated caller's facility.
public interface IPatientsOfInterestService
{
    // Cerner. Tests the given connection and caches the files it returns for GetSftpFilesAsync.
    Task<ConnectionResult> TestSftpConnectionAsync(SftpConfig config, CancellationToken cancellationToken = default);

    // Cerner. Serves the last TestSftpConnectionAsync's cached result — empty until one has run.
    Task<IReadOnlyList<SftpFile>> GetSftpFilesAsync(CancellationToken cancellationToken = default);

    // Epic.
    Task<CensusListResult> QueryPatientListAsync(string listKey, CancellationToken cancellationToken = default);

    Task AcknowledgeCensusAsync(AcknowledgementRequest request, CancellationToken cancellationToken = default);
}
