using LantanaGroup.Link.Submission.Domain.Models;

namespace LantanaGroup.Link.Submission.Application.Services.Interfaces;

public interface IAuth
{
    Task<(bool isQueryParam, object authHeaderValue)> SetAuthentication(AuthenticationConfiguration authSettings);
}

