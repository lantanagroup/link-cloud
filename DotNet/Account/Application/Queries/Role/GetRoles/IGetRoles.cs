using LantanaGroup.Link.Account.Application.Models.Role;

namespace LantanaGroup.Link.Account.Application.Queries.Role
{
    public interface IGetRoles
    {
        Task<IEnumerable<ListRoleModel>> Execute(CancellationToken cancellationToken = default);
    }
}
