using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services;

public class UserAdministrationService : IUserAdministrationService
{
    private readonly NhsnAppDbContext _dbContext;

    public UserAdministrationService(NhsnAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<UserRoleSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Select(user => new UserRoleSummaryResponse
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                FacilityId = user.FacilityId,
                IsOnboarded = user.IsOnboarded,
                IsActive = user.IsActive,
                IsAdmin = user.IsAdmin,
                Groups = string.IsNullOrWhiteSpace(user.GroupsRaw)
                    ? Array.Empty<string>()
                    : user.GroupsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<UserRoleSummaryResponse?> UpdateUserAdminAsync(Guid userId, string actingExternalUserId, UpdateUserAdminRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (string.Equals(user.ExternalUserId, actingExternalUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("System administrators cannot change their own admin flag.");
        }

        user.IsAdmin = request.IsAdmin;

        user.LastModifiedBy = "user-admin";
        user.LastModifiedOn = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserRoleSummaryResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            FacilityId = user.FacilityId,
            IsOnboarded = user.IsOnboarded,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            Groups = string.IsNullOrWhiteSpace(user.GroupsRaw)
                ? Array.Empty<string>()
                : user.GroupsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
    }

    public async Task<UserRoleSummaryResponse?> UpdateUserStatusAsync(Guid userId, string actingExternalUserId, UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!request.IsActive && string.Equals(user.ExternalUserId, actingExternalUserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("System administrators cannot disable their own account.");
        }

        user.IsActive = request.IsActive;
        user.LastModifiedBy = "user-admin";
        user.LastModifiedOn = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UserRoleSummaryResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            FacilityId = user.FacilityId,
            IsOnboarded = user.IsOnboarded,
            IsActive = user.IsActive,
            IsAdmin = user.IsAdmin,
            Groups = string.IsNullOrWhiteSpace(user.GroupsRaw)
                ? Array.Empty<string>()
                : user.GroupsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
    }
}