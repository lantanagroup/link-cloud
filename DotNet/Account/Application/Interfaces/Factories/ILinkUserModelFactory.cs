using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Interfaces.Factories
{
    public interface ILinkUserModelFactory
    {
        LinkUserModel Create(LinkUser user);
        LinkUserModel Create(string userId, string? username, string? email, string? firstName, string? lastName, string? middleName, List<string>? facilities, List<string>? roles, List<string>? claims);
    }
}
