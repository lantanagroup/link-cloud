using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class FacilityServiceClient : LinkApiClientBase, IFacilityServiceClient
{
    private readonly IOptions<ServiceRegistry> _serviceRegistry;

    public FacilityServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.TenantServiceApiUrl
                ?? throw new InvalidOperationException("Tenant service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    {
        _serviceRegistry = serviceRegistry;
    }

    public Task<LinkApiResponse<FacilityModel>> CreateAsync(
        FacilityModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityModel>(() => Request("/Facility")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityModel>> GetAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityModel>(() => Request($"/Facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityModel>> UpdateAsync(
        string facilityId,
        FacilityModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityModel>(() => Request($"/Facility/{facilityId}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> CheckFacilityExistsAsync(
        string facilityId,
        CancellationToken cancellationToken = default)
    {
        if (_serviceRegistry.Value.TenantService?.CheckIfTenantExists != true)
            return Task.FromResult(new LinkApiResponse { StatusCode = 200 });

        return SendAsync(() => Request($"/Facility/{facilityId}")
            .GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> DeleteAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/Facility/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SoftDeleteAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/Facility/softDelete/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> RestoreAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/Facility/restore/{facilityId}")
            .PatchAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SearchFacilitiesAsync(
        string? facilityId = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var req = Request("/Facility")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber);
        if (!string.IsNullOrWhiteSpace(facilityId))
            req = req.SetQueryParam("facilityId", facilityId);
        return SendAsync(() => req.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<Dictionary<string, string>>> GetFacilityListAsync(
        string? search = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var req = Request("/Facility/list")
            .SetQueryParam("includeDeleted", includeDeleted);
        if (!string.IsNullOrWhiteSpace(search))
            req = req.SetQueryParam("search", search);
        return SendAsync<Dictionary<string, string>>(() => req.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse<GenerateAdhocReportResponseApiModel>> GenerateAdhocReportAsync(
        string facilityId,
        AdHocReportRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<GenerateAdhocReportResponseApiModel>(() => Request($"/Facility/{facilityId}/AdHocReport")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<GenerateAdhocReportResponseApiModel>> RegenerateReportAsync(
        string facilityId,
        RegenerateReportRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<GenerateAdhocReportResponseApiModel>(() => Request($"/Facility/{facilityId}/RegenerateReport")
            .PostJsonAsync(request, cancellationToken: cancellationToken));
}
