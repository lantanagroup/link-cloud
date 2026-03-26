using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Factories.Role
{
    public class ListRoleModelFactory : IListRoleModelFactory
    {
        public ListRoleModel Create(LinkRole role)
        {
            return new ListRoleModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Description = role.Description ?? string.Empty             
            };
        }

        public ListRoleModel Create(LinkRoleModel role)
        {
            return new ListRoleModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Description = role.Description ?? string.Empty             
            };
        }
    }
}
