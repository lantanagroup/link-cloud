using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using System.Net.Http.Headers;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class BasicAuth : IAuth
{
    private readonly ISecretManager _secretManager;

    public BasicAuth(ISecretManager secretManager)
    {
        _secretManager = secretManager;
    }

    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        char[]? credentialsArray = null;

        try
        {
            if (string.IsNullOrWhiteSpace(authSettings.UserName))
                throw new ArgumentException("A secret name for UserName must be provided for Basic authentication.");
            if (string.IsNullOrWhiteSpace(authSettings.Password))
                throw new ArgumentException("A secret name for Password must be provided for Basic authentication.");

            var userName = await _secretManager.GetSecretAsync(authSettings.UserName, CancellationToken.None);
            var password = await _secretManager.GetSecretAsync(authSettings.Password, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new InvalidOperationException($"No value found in secret manager for UserName");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException($"No value found in secret manager for Password");
            }


            credentialsArray = $"{userName}:{password}".ToCharArray();

            return (false,
                new AuthenticationHeaderValue("basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialsArray))));
        }
        finally
        {
            ClearSensitiveData(credentialsArray);
        }
    }

    private static void ClearSensitiveData(char[]? sensitiveData)
    {
        if (sensitiveData == null) return;
        Array.Clear(sensitiveData, 0, sensitiveData.Length);
    }
}