using LantanaGroup.Link.Account.Application.Models.User;
using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Commands.User
{
    public interface IRecoverUser
    {
        Task<LinkUserModel> Execute(ClaimsPrincipal? requestor, Guid userId, CancellationToken cancellationToken = default);
    }
}
