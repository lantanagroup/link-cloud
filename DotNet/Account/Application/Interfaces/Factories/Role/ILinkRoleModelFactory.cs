using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Interfaces.Factories.Role
{
    public interface ILinkRoleModelFactory
    {
        LinkRoleModel Create(LinkRole role);
        LinkRoleModel Create(string name, string description, IEnumerable<string> claims);
    }
}
