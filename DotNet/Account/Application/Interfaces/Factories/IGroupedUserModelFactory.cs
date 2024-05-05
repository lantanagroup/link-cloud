using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Interfaces.Factories
{
    public interface IGroupedUserModelFactory
    {
        GroupedUserModel Create(LinkUser user);
        GroupedUserModel Create(LinkUserModel user);
        GroupedUserModel Create(string userId, string? username, string? email, string? firstName, string? lastName, string? middleName);
    }
}
