using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Cerner's sFTP connection tests, both run by Data Acquisition.
public interface ISftpFileGateway
{
    // Tests the facility's already-saved configuration and credentials. Returns no file listing;
    // the caller is responsible for saving the configuration first.
    Task<ConnectionResult> TestConnectionAsync(string facilityId, CancellationToken cancellationToken = default);

    // Tests the given connection details without saving anything, and previews every file in the
    // report directory with the patients it contains. Needs the username and password, so it can
    // only run while the caller still has them.
    Task<SftpPreviewResult> TestConnectionWithPreviewAsync(SftpConfig config, CancellationToken cancellationToken = default);
}

public sealed record SftpPreviewResult(ConnectionResult Result, IReadOnlyList<SftpFile> Files);
