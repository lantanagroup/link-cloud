using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Session;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IUserInfoService
{
    /// <summary>
    /// Resolves the caller's NHSNLink context. Takes no principal — facility and role come from
    /// <see cref="INhsnUserContext"/>, which reads the validated token.
    /// </summary>
    Task<UserInfoResponse> GetUserInfoAsync(CancellationToken cancellationToken = default);
}
