using LantanaGroup.Link.Account.Application.Models.Role;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public interface ICreateRole
    {
        Task<LinkRoleModel> Execute(ClaimsPrincipal? requestor, LinkRoleModel model, CancellationToken cancellationToken = default);
    }
}
