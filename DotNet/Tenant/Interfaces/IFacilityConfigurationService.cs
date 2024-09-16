using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Interfaces
{
    public interface IFacilityConfigurationService
    {
        Task CreateFacility(FacilityConfigModel newFacility, CancellationToken cancellationToken);
        Task<List<FacilityConfigModel>> GetAllFacilities(CancellationToken cancellationToken = default);
        Task<PagedConfigModel<FacilityConfigModel>> GetFacilities(string? facilityId, string? facilityName, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
        Task<FacilityConfigModel> GetFacilityByFacilityId(string facilityId, CancellationToken cancellationToken);
        Task<FacilityConfigModel> GetFacilityById(string id, CancellationToken cancellationToken);
        Task<string> RemoveFacility(string facilityId, CancellationToken cancellationToken);
        Task<string> UpdateFacility(string id, FacilityConfigModel newFacility, CancellationToken cancellationToken = default);
    }
}