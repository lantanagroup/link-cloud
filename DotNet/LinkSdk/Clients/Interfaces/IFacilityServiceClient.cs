using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Tenant;

namespace LantanaGroup.Link.Sdk.Clients;

public interface IFacilityServiceClient
{
    Task<FacilityModel> CreateAsync(FacilityModel request, CancellationToken cancellationToken = default);
    Task<FacilityModel?> GetAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<bool> CheckFacilityExistsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<GenerateAdhocReportResponseApiModel> GenerateAdhocReportAsync(string facilityId, AdHocReportRequest request, CancellationToken cancellationToken = default);
    Task<GenerateAdhocReportResponseApiModel> RegenerateReportAsync(string facilityId, RegenerateReportRequest request, CancellationToken cancellationToken = default);
}
