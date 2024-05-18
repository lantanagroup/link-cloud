using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Interfaces.Factories.Role
{
    public interface IListRoleModelFactory
    {
        ListRoleModel Create(LinkRole role);
        ListRoleModel Create(LinkRoleModel role);
    }
}
