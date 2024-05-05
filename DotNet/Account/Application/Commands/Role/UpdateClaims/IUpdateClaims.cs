using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role.UpdateClaims
{
    public interface IUpdateClaims
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, string roleId, List<string> claims, CancellationToken cancellationToken = default);
    }
}
