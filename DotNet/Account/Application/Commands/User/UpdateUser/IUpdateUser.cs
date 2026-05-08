using LantanaGroup.Link.Account.Application.Models.User;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IUpdateUser
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default);
    }
}
