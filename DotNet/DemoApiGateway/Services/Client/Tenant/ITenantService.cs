using LantanaGroup.Link.DemoApiGateway.Application.models;

namespace LantanaGroup.Link.DemoApiGateway.Services.Client
{
    public interface ITenantService
    {
        Task<HttpResponseMessage> GetFacility(string facilityId);       
        Task<HttpResponseMessage> CreateFacility(FacilityConfigModel model);
        Task<HttpResponseMessage> UpdateFacility(string facilityId, FacilityConfigModel model);
        Task<HttpResponseMessage> DeleteFacility(string facilityId);
        Task<HttpResponseMessage> ListFacilities(string? facilityId, string? facilityName, string? sortBy, int pageSize = 10, int pageNumber = 1);
    }
}
