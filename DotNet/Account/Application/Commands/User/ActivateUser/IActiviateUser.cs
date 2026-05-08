using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IActiviateUser
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, Guid userId, CancellationToken cancellationToken = default);
    }
}
