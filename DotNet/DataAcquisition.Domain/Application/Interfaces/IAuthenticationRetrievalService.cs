using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Services.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;

public interface IAuthenticationRetrievalService
{
    IAuth GetAuthenticationService(AuthenticationConfiguration authenticationSettings);
}
