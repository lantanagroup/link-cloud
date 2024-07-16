using LantanaGroup.Link.DataAcquisition.Domain.Models;

namespace LantanaGroup.Link.DataAcquisition.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(AuthenticationConfiguration authSettings);
}

