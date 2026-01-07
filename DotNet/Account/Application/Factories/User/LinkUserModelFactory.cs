using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Factories.User
{
    public class LinkUserModelFactory : ILinkUserModelFactory
    {
        public LinkUserModel Create(LinkUser user)
        {
            LinkUserModel model = new()
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName,   
                IsDeleted = user.IsDeleted,
                IsActive = user.IsActive,
                Roles = user.UserRoles.Select(r => r.Role.Name ?? string.Empty).ToList() ?? [],
                UserClaims = user.Claims.Select(c => c.ClaimValue ?? string.Empty).ToList() ?? [],
                RoleClaims = user.UserRoles.SelectMany(r => r.Role.RoleClaims).Select(c => c.ClaimValue ?? string.Empty).Distinct().ToList() ?? []
            };

            return model;
        }

        public LinkUserModel Create(Guid userId, string? username, string? email, string? firstName, string? lastName, string? middleName, List<string>? roles, List<string>? userClaims, List<string>? roleClaims)
        {
            LinkUserModel model = new()
            {
                Id = userId,
                Username = username ?? string.Empty,
                Email = email ?? string.Empty,
                FirstName = firstName ?? string.Empty,
                LastName = lastName ?? string.Empty,
                MiddleName = middleName ?? string.Empty,
                Roles = roles ?? [],
                UserClaims = userClaims ?? [],
                RoleClaims = roleClaims ?? []
            };

            return model;
        }
    }
}
