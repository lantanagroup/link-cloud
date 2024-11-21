using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IUpdateUserClaims
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, Guid userId, List<string> claims, CancellationToken cancellationToken = default);
    }
}
