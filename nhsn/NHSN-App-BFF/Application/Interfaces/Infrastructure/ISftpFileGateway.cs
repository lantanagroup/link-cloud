using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Cerner's sFTP file listing, one call: Data Acquisition's ad-hoc test-connection endpoint
// returns every file with its patients already attached when includeFileContent=true.
//
// Fixture-only for now: LinkSdk has no sFTP coverage at all, so there is nothing to call through.
// This is decided SDK work, not a capability — the shape is fully specified, nobody is waited on.
// Every implementation must set Simulated = true until a real adapter exists to replace it.
public interface ISftpFileGateway
{
    Task<IReadOnlyList<SftpFile>> ListFilesAsync(string facilityId, CancellationToken cancellationToken = default);
}
