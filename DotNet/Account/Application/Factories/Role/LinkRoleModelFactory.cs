using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Models.Role;
using LantanaGroup.Link.Account.Domain.Entities;
using Link.Authorization.Infrastructure;

namespace LantanaGroup.Link.Account.Application.Factories.Role
{
    public class LinkRoleModelFactory : ILinkRoleModelFactory
    {
        public LinkRoleModel Create(LinkRole role)
        {
            return new LinkRoleModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Description = role.Description ?? string.Empty,
                Claims = role.RoleClaims
                    .Where(x => x.ClaimValue is not null && x.ClaimType == LinkAuthorizationConstants.LinkSystemClaims.LinkPermissions)
                    .Select(x => x.ClaimValue ?? string.Empty).ToList()
            };
            
        }

        public LinkRoleModel Create(string name, string description, IEnumerable<string> claims)
        {
            return new LinkRoleModel
            {
                Name = name,
                Description = description,
                Claims = claims.ToList()
            };
        }
    }
}
