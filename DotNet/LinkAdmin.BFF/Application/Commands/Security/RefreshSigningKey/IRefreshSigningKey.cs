using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public interface IRefreshSigningKey
    {
        Task<bool> ExecuteAsync(ClaimsPrincipal user);
    }
}
