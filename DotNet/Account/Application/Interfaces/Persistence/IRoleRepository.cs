using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface IRoleRepository
    {
        Task<LinkRole> GetRoleAsync(string roleId, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<IEnumerable<LinkRole>> GetRolesAsync(CancellationToken cancellationToken = default);
    }
}
