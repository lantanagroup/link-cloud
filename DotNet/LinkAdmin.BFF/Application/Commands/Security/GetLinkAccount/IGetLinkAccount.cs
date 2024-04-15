using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Security;
using System.Security.Claims;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Commands.Security
{
    public interface IGetLinkAccount
    {
        Task<Account?> ExecuteAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    }
}
