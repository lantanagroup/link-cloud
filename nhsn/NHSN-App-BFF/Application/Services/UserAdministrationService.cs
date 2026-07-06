using System.Linq.Expressions;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;
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
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.Email)
            .Select(MapProjection())
            .ToListAsync(cancellationToken);
    }

    public async Task<UserRoleSummaryResponse?> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var normalizedRoles = request.Roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roles = await _dbContext.Roles
            .Where(x => normalizedRoles.Contains(x.Name))
            .ToListAsync(cancellationToken);

        user.UserRoles.Clear();
        foreach (var role in roles)
        {
            user.UserRoles.Add(new NhsnUserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                User = user,
                Role = role
            });
        }

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
            Roles = user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray()
        };
    }

    private static Expression<Func<NhsnUser, UserRoleSummaryResponse>> MapProjection()
    {
        return user => new UserRoleSummaryResponse
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            FacilityId = user.FacilityId,
            IsOnboarded = user.IsOnboarded,
            Roles = user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray()
        };
    }
}