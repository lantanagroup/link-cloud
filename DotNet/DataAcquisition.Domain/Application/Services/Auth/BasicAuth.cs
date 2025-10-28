using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using System.Net.Http.Headers;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class BasicAuth : IAuth
{
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        char[]? credentialsArray = null;

        try
        {
            credentialsArray = $"{authSettings.UserName}:{authSettings.Password}".ToCharArray();

            var pw = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialsArray));

            return (false,
                new AuthenticationHeaderValue("basic", pw));
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