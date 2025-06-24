using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Domain.Factories.Auth;

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
