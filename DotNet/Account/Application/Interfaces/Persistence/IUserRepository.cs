using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface IUserRepository
    {
        Task<bool> CreateAsync(LinkUser entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(LinkUser entity, CancellationToken cancellationToken = default);
        Task<bool> AddRoleAsync(Guid userId, LinkRole role, CancellationToken cancellationToken = default);
        Task<bool> AddRolesAsync(Guid userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default);
        Task<bool> RemoveRoleAsync(Guid userId, LinkRole role, CancellationToken cancellationToken = default);
        Task<bool> RemoveRolesAsync(Guid userId, IEnumerable<LinkRole> roles, CancellationToken cancellationToken = default);
        Task<bool> AddClaimToUserAsync(Guid userId, Claim claim, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimFromUserAsync(Guid userId, Claim claim, CancellationToken cancellationToken = default);
        Task<bool> AddClaimsToUserAsync(Guid userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimsFromUserAsync(Guid userId, IEnumerable<Claim> claims, CancellationToken cancellationToken = default);
        Task<LinkUser> GetUserAsync(Guid id, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<LinkUser> GetUserByEmailAsync(string email, bool noTracking = true, CancellationToken cancellationToken = default);
        //Task<IEnumerable<LinkUser>> GetFacilityUsersAsync(string facilityId, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkUser>> GetRoleUsersAsync(string role, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkRole>> GetUserRoles(Guid userId, CancellationToken cancellationToken = default);
    }
}
