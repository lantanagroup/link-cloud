using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Entities;
using System.Net.Http.Headers;

namespace LantanaGroup.Link.DataAcquisition.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(AuthenticationConfiguration authSettings);
}

