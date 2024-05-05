using LantanaGroup.Link.Account.Application.Models.Role;

namespace LantanaGroup.Link.Account.Application.Commands.Role
{
    public interface ICreateRole
    {
        Task<LinkRoleModel> Execute(string name, string? description, List<string>? claims, CancellationToken cancellationToken = default);
    }
}
