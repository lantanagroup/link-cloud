using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(string facilityId, AuthenticationConfiguration authSettings);
}

