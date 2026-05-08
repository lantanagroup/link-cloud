using LantanaGroup.Link.Account.Domain.Entities;
using System.Security.Claims;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface IRoleRepository
    {
        Task<bool> CreateAsync(LinkRole entity, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(LinkRole entity, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> AddClaimAsync(Guid roleId, Claim claim, CancellationToken cancellationToken = default);
        Task<bool> RemoveClaimAsync(Guid roleId, Claim claim, CancellationToken cancellationToken = default);
        Task<LinkRole> GetRoleAsync(Guid roleId, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<LinkRole> GetRoleByNameAsync(string roleName, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkRole>> GetRolesAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<Claim>> GetClaimsAsync(Guid roleId, CancellationToken cancellationToken = default);
    }
}
