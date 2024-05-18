using LantanaGroup.Link.Account.Application.Models.User;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface ICreateUser
    {
        Task<LinkUserModel> Execute(ClaimsPrincipal? requestor, LinkUserModel model, CancellationToken cancellationToken = default);
    }
}
