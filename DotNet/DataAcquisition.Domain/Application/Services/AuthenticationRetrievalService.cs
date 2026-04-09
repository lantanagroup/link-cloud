using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Auth;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public class AuthenticationRetrievalService : IAuthenticationRetrievalService
{
    private readonly EpicAuth _epicAuth;
    private readonly BasicAuth _basicAuth;
    private readonly CustomHeaderAuth _customHeaderAuth;

    public AuthenticationRetrievalService(EpicAuth epicAuth, BasicAuth basicAuth, CustomHeaderAuth customHeaderAuth)
    {
        _epicAuth = epicAuth;
        _basicAuth = basicAuth;
        _customHeaderAuth = customHeaderAuth;
    }

    public IAuth GetAuthenticationService(AuthenticationConfigurationModel authenticationSettings)
    {
        if (authenticationSettings == null) return null;

        IAuth? service = authenticationSettings?.AuthType switch
        {
            nameof(AuthType.Epic) => _epicAuth,
            nameof(AuthType.Basic) => _basicAuth,
            nameof(AuthType.CustomHeaders) => _customHeaderAuth,
            _ => null,
        };
        return service;
    }
}
