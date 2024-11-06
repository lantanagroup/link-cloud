using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Factories.User
{
    public class GroupedUserModelFactory : IGroupedUserModelFactory
    {
        public GroupedUserModel Create(LinkUser user)
        {
            GroupedUserModel model = new()
            {
                Id = user.Id,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName ?? string.Empty,
            };

            return model;
        }

        public GroupedUserModel Create(LinkUserModel user)
        {
            GroupedUserModel model = new()
            {
                Id = user.Id,
                Username = user.Username ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName ?? string.Empty,
            };

            return model;
        }

        public GroupedUserModel Create(Guid userId, string? username, string? email, string? firstName, string? lastName, string? middleName)
        {
            GroupedUserModel model = new()
            {
                Id = userId,
                Username = username ?? string.Empty,
                Email = email ?? string.Empty,
                FirstName = firstName ?? string.Empty,
                LastName = lastName ?? string.Empty,
                MiddleName = middleName ?? string.Empty,
            };

            return model;
        }
    }
}
