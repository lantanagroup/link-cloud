using LantanaGroup.Link.Submission.Application.Services.Interfaces;
using LantanaGroup.Link.Submission.Domain.Models;

namespace LantanaGroup.Link.Submission.Application.Services.Auth;

public class BasicAuth : IAuth
{
    public async Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(AuthenticationConfiguration authSettings)
    {
        var authenticationString = $"{authSettings.UserName}:{authSettings.Password}";
        var encodedString = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(authenticationString));
        return (false, encodedString);
    }
}
