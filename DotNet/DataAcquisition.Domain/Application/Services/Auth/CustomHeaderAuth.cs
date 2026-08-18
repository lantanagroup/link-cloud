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
            var name = header.Value;
            var value = await _secretManager.GetSecretAsync(name, CancellationToken.None);
            if (string.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(
                    $"Secret for custom auth header '{header.Key}' (secret name: '{name}') could not be resolved or is empty.");
            }
            headers.Add(header.Key, value);
        }

        return (false, headers);
    }
}
