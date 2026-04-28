using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class CustomHeaderAuth : IAuth
{
    private readonly ISecretManager _secretManager;

    public CustomHeaderAuth(ISecretManager secretManager)
    {
        _secretManager = secretManager;
    }

    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        if (authSettings.CustomHeaders == null || !authSettings.CustomHeaders.Any())
        {
            return (false, new Dictionary<string, string>());
        }

        var headers = new Dictionary<string, string>();
        foreach (var header in authSettings.CustomHeaders)
        {
            var secretName = header.Value;
            var secretValue = await _secretManager.GetSecretAsync(secretName, CancellationToken.None);
            headers.Add(header.Key, secretValue ?? "");
        }

        return (false, headers);
    }
}
