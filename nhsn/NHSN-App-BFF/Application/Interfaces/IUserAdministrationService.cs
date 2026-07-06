using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;

public interface IUserAdministrationService
{
    Task<IReadOnlyCollection<UserRoleSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserRoleSummaryResponse?> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);
}