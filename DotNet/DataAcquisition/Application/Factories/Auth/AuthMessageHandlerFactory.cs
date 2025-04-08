using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Factories.Auth;

public class AuthMessageHandlerFactory
{
    public static async Task<(bool isQueryParam, object? authHeader)> Build(string facilityId, IAuthenticationRetrievalService authenticationRetrievalService, AuthenticationConfiguration config)
    {
        (bool isQueryParam, object authHeader) authHeader = (false, null);
        IAuth authService = authenticationRetrievalService.GetAuthenticationService(config);

        if(authService == null)
            return (false, null);

        authHeader = await authService.SetAuthentication(facilityId, config);
        return authHeader;
    }
}
