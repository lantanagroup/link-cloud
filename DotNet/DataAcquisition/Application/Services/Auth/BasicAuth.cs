using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Entities;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Services.Auth;

public class BasicAuth : IAuth
{
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(AuthenticationConfiguration authSettings)
    {
        var authenticationString = $"{authSettings.UserName}:{authSettings.Password}";
        var encodedString = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(authenticationString));
        return (false, encodedString);
    }
}
