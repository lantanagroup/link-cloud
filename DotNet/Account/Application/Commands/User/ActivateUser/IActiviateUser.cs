using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User.ActivateUser
{
    public interface IActiviateUser
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, string userId, CancellationToken cancellationToken = default);
    }
}
