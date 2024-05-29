using System.Security.Claims;

namespace LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token
{
    internal interface ICreateUserToken
    {
        Task<string> ExecuteAsync(ClaimsPrincipal user, string key, int timespan);
    }
}
