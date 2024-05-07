using LantanaGroup.Link.Account.Application.Models.Role;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public interface IUpdateRole
    {
        Task<bool> Execute(ClaimsPrincipal? requestor, LinkRoleModel role, CancellationToken cancellationToken = default);
    }
}
