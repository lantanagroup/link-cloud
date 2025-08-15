using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Interfaces
{
    public interface IFacilityConfigurationService
    {
        Task CreateFacility(Facility newFacility, CancellationToken cancellationToken);
        Task<List<Facility>> GetAllFacilities(CancellationToken cancellationToken = default);
        Task<PagedConfigModel<Facility>> GetFacilities(string? facilityId, string? facilityName, string? sortBy, SortOrder? sortOrder, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
        Task<Facility?> GetFacilityByFacilityId(string facilityId, CancellationToken cancellationToken);
        Task<Facility> GetFacilityById(Guid id, CancellationToken cancellationToken);
        Task<string> RemoveFacility(string facilityId, CancellationToken cancellationToken);
        Task<string> UpdateFacility(Guid id, Facility newFacility, CancellationToken cancellationToken = default);
        Task MeasureDefinitionExists(String reportType);
    }
}