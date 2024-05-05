using LantanaGroup.Link.Account.Application.Interfaces.Factories;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Factories
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
                Facilities = user.Facilities ?? [],
                Roles = user.UserRoles.Select(r => r.Role.Name ?? string.Empty).ToList() ?? [],
                Claims = user.Claims.Select(c => c.ClaimValue ?? string.Empty).ToList() ?? [],
            };

            return model;            
        }

        public LinkUserModel Create(string userId, string? username, string? email, string? firstName, string? lastName, string? middleName, List<string>? facilities, List<string>? roles, List<string>? claims)
        {
            LinkUserModel model = new()
            {
                Id = userId,
                Username = username ?? string.Empty,
                Email = email ?? string.Empty,
                FirstName = firstName ?? string.Empty,
                LastName = lastName ?? string.Empty,
                MiddleName = middleName ?? string.Empty,
                Facilities = facilities ?? [],
                Roles = roles ?? [],
                Claims = claims ?? []
            };

            return model;
        }        
    }
}
