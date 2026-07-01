using System.Security.Claims;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

public interface IUserInfoService
{
    Task<UserInfoResponse> GetUserInfoAsync(ClaimsPrincipal principal, HttpRequest request, CancellationToken cancellationToken = default);
}