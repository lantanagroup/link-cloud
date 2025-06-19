using DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Services;

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
        if (authenticationSettings == null) return null;

        IAuth? service = authenticationSettings?.AuthType switch
        {
            nameof(AuthType.Epic) => _epicAuth,
            nameof(AuthType.Basic) => _basicAuth,
            _ => null,
        };
        return service;
    }
}
