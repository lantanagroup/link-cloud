using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

public interface IUserAdministrationService
{
    Task<IReadOnlyCollection<UserRoleSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserRoleSummaryResponse?> UpdateUserRolesAsync(Guid userId, string actingExternalUserId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);
    Task<UserRoleSummaryResponse?> UpdateUserStatusAsync(Guid userId, string actingExternalUserId, UpdateUserStatusRequest request, CancellationToken cancellationToken = default);
}