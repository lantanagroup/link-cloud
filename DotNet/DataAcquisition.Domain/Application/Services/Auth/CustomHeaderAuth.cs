using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;

public class CustomHeaderAuth : IAuth
{
    public Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings)
    {
        if (authSettings.CustomHeaders == null || !authSettings.CustomHeaders.Any())
        {
            return Task.FromResult<(bool, object)>((false, null));
        }

        return Task.FromResult<(bool, object)>((false, authSettings.CustomHeaders));
    }
}
