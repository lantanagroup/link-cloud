using LantanaGroup.Link.Account.Application.Models.Role;

namespace LantanaGroup.Link.Account.Application.Queries.Role.GetRoles
{
    public interface IGetRoles
    {
        Task<IEnumerable<ListRoleModel>> Execute(CancellationToken cancellationToken = default);
    }
}
