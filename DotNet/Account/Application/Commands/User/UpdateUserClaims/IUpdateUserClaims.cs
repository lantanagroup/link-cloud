using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IUpdateUserClaims
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, string userId, List<string> claims, CancellationToken cancellationToken = default);
    }
}
