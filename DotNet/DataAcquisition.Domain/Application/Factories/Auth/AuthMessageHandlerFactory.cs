using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Factories.Auth;

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
