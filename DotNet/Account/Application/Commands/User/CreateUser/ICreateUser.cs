using LantanaGroup.Link.Account.Application.Models;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User.CreateUser
{
    public interface ICreateUser
    {
        Task<LinkUserModel> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default);
    }
}
