using DataAcquisition.Domain.Application.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfigurationModel authSettings);
}

