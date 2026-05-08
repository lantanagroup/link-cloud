using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;

public interface IAuthenticationRetrievalService
{
    IAuth GetAuthenticationService(AuthenticationConfigurationModel authenticationSettings);
}
