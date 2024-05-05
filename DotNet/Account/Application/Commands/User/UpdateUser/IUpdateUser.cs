using LantanaGroup.Link.Account.Application.Models;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User.UpdateUser
{
    public interface IUpdateUser
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default);
    }
}
