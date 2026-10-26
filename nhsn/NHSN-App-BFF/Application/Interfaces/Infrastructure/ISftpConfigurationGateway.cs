using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;

// Cerner's persisted sFTP configuration and credentials — two separate Data Acquisition resources.
// The configuration resource (host/port/remoteDirectory/removeAfterProcessing) carries no
// credentials fields; credentials are a write-only resource of their own, read back only as a
// hasCredentials/lastUpdated status, never the values.
//
// Fixture-only for now: LinkSdk has no sFTP coverage at all, so there is nothing to call through.
// NHSN-App-BFF does not touch DotNet/LinkSdk, so this is the platform team's work, not ours.
public interface ISftpConfigurationGateway
{
    Task<SftpConfig?> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveConfigurationAsync(string facilityId, SftpConfig config, CancellationToken cancellationToken = default);

    Task SaveCredentialsAsync(string facilityId, string username, string password, CancellationToken cancellationToken = default);

    Task<bool> GetHasCredentialsAsync(string facilityId, CancellationToken cancellationToken = default);
}
