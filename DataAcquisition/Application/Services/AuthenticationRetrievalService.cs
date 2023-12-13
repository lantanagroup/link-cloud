using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Services;

public class AuthenticationRetrievalService : IAuthenticationRetrievalService
{
    private readonly EpicAuth _epicAuth;
    private readonly BasicAuth _basicAuth;

    public AuthenticationRetrievalService(EpicAuth epicAuth, BasicAuth basicAuth)
    {
        _epicAuth = epicAuth;
        _basicAuth = basicAuth;
    }

    public IAuth GetAuthenticationService(AuthenticationConfiguration authenticationSettings)
    {
        IAuth? service = authenticationSettings?.AuthType switch
        {
            nameof(AuthType.Epic) => _epicAuth,
            nameof(AuthType.Basic) => _basicAuth,
            _ => null,
        };
        return service;
    }
}
