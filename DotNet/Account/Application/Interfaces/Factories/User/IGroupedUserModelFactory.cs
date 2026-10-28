using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Interfaces.Factories.User
{
    public interface IGroupedUserModelFactory
    {
        GroupedUserModel Create(LinkUser user);
        GroupedUserModel Create(LinkUserModel user);
        GroupedUserModel Create(Guid userId, string? username, string? email, string? firstName, string? lastName, string? middleName, bool? isActive, bool? isDeleted);
    }
}
