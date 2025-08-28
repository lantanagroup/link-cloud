using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using System.Net.Http.Headers;
using System.Text;

namespace LantanaGroup.Link.DataAcquisition.Domain.Services.Auth;

public class BasicAuth : IAuth
{
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfiguration authSettings)
    {
        if(authSettings == null)
            throw new ArgumentNullException(nameof(authSettings), "Authentication settings cannot be null.");

        char[]? credentialsArray = null;

        try
        {
            if (string.IsNullOrEmpty(authSettings.UserName))
                throw new ArgumentException("Username cannot be null or empty.", nameof(authSettings.UserName));
            if (authSettings.Password == null || authSettings.Password.Length == 0)
                throw new ArgumentException("Password cannot be null or empty.", nameof(authSettings.Password));
            if (authSettings.UserName.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new ArgumentException("Username must not contain control characters.", nameof(authSettings.UserName));
            
            credentialsArray = $"{authSettings.UserName}:{authSettings.Password}".ToCharArray();
            var pw = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentialsArray));

            return (false, new AuthenticationHeaderValue(DataAcquisitionConstants.Auth.Basic, pw));
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
