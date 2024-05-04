using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IRecoverUser
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, string userId, CancellationToken cancellationToken = default);
    }
}
