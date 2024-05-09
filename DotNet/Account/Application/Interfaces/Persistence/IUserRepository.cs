using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface IUserRepository
    {
        Task<bool> CreateAsync(LinkUser entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(LinkUser entity, CancellationToken cancellationToken = default);
        Task<bool> AddRoleAsync(string userId, LinkRole role, CancellationToken cancellationToken = default);
        Task<bool> AddRolesAsync(string userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default);
        Task<bool> RemoveRoleAsync(string userId, LinkRole role, CancellationToken cancellationToken = default);
        Task<bool> RemoveRolesAsync(string userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default);
        Task<bool> AddClaimToUserAsync(string userId, Claim claim, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimFromUserAsync(string userId, Claim claim, CancellationToken cancellationToken = default);
        Task<bool> AddClaimsToUserAsync(string userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimsFromUserAsync(string userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
        Task<LinkUser> GetUserAsync(string id, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<LinkUser> GetUserByEmailAsync(string email, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkUser>> GetFacilityUsersAsync(string facilityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkUser>> GetRoleUsersAsync(string role, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkRole>> GetUserRoles(string userId, CancellationToken cancellationToken = default);
    }
}
