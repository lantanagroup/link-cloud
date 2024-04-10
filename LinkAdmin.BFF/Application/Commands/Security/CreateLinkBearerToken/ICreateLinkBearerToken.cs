using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public interface ICreateLinkBearerToken
    {
        Task<string> ExecuteAsync(ClaimsPrincipal user, int timespan);
    }
}
