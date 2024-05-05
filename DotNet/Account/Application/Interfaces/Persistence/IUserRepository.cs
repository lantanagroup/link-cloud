using LantanaGroup.Link.Account.Domain.Entities;

namespace LantanaGroup.Link.Account.Application.Interfaces.Persistence
{
    public interface IUserRepository
    {
        Task<LinkUser> GetUserAsync(string id, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<LinkUser> GetUserByEmailAsync(string email, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<List<LinkUser>> GetFacilityUsersAsync(string facilityId, bool noTracking = true, CancellationToken cancellationToken = default);
        Task<List<LinkUser>> GetRoleUsersAsync(string role, bool noTracking = true, CancellationToken cancellationToken = default);
    }
}
