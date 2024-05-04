using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IRecoverUser
    {
        Task<LinkUser> Execute(ClaimsPrincipal? requestor, string userId, CancellationToken cancellationToken = default);
    }
}
