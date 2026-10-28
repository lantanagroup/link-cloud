using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IDeactivateUser
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, Guid userId, CancellationToken cancellationToken = default);
    }
}
