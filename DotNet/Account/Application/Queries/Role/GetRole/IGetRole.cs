using LantanaGroup.Link.Account.Application.Models.Role;

namespace LantanaGroup.Link.Account.Application.Queries.Role.GetRole
{
    public interface IGetRole
    {
        Task<LinkRoleModel> Execute(string roleId, CancellationToken cancellationToken = default);
    }
}
