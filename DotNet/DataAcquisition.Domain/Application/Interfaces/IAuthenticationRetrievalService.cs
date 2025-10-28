using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;

public interface IAuthenticationRetrievalService
{
    IAuth GetAuthenticationService(AuthenticationConfiguration authenticationSettings);
}
