using LantanaGroup.Link.Account.Application.Models.Role;

namespace LantanaGroup.Link.Account.Application.Queries.Role
{
    public interface IGetRoleByName
    {
        Task<LinkRoleModel> Execute(string name, CancellationToken cancellationToken = default);
    }
}
