using System.Collections.Concurrent;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Capabilities;

// Registered unconditionally — there is no real adapter to branch to yet, since LinkSdk has no
// sFTP coverage. In-memory only: config and credential presence do not survive a restart, same
// lifetime as every other fixture in this namespace. Credentials themselves are never retained,
// even here — only whether SaveCredentialsAsync was called, matching the real credentials/status
// endpoint's own shape.
internal sealed class SftpConfigurationFixtureGateway : ISftpConfigurationGateway
{
    private readonly ConcurrentDictionary<string, SftpConfig> _configs = new();
    private readonly ConcurrentDictionary<string, bool> _hasCredentials = new();

    public Task<SftpConfig?> GetConfigurationAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_configs.GetValueOrDefault(facilityId));

    public Task SaveConfigurationAsync(string facilityId, SftpConfig config, CancellationToken cancellationToken = default)
    {
        _configs[facilityId] = config;
        return Task.CompletedTask;
    }

    public Task SaveCredentialsAsync(string facilityId, string username, string password, CancellationToken cancellationToken = default)
    {
        _hasCredentials[facilityId] = true;
        return Task.CompletedTask;
    }

    public Task<bool> GetHasCredentialsAsync(string facilityId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_hasCredentials.GetValueOrDefault(facilityId));
}
