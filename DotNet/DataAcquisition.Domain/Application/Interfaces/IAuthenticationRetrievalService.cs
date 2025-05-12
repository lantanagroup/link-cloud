using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;

public interface IAuthenticationRetrievalService
{
    IAuth GetAuthenticationService(AuthenticationConfiguration authenticationSettings);
}
