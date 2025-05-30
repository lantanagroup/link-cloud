using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;
using LantanaGroup.Link.Shared.Application.Services.Security;
using System.Net.Http.Headers;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Services.Auth;

public class BasicAuth : IAuth
{
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfiguration authSettings)
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
