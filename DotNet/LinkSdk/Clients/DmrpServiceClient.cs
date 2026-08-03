using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class DmrpServiceClient : LinkApiClientBase, IDmrpServiceClient
{
    public DmrpServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.DmrpServiceApiUrl
                ?? throw new InvalidOperationException("DMRP service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    {
    }

    public Task<LinkApiResponse<MeasureMappingModel>> CreateMeasureMappingAsync(
        MeasureMappingModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeasureMappingModel>(() => Request("/dmrp/measure-mappings")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<MeasureMappingModel>> GetMeasureMappingAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeasureMappingModel>(() => Request($"/dmrp/measure-mappings/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<MeasureMappingModel>> UpdateMeasureMappingAsync(
        string id,
        MeasureMappingModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<MeasureMappingModel>(() => Request($"/dmrp/measure-mappings/{id}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteMeasureMappingAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/dmrp/measure-mappings/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SearchMeasureMappingsAsync(
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("/dmrp/measure-mappings")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityReportingPlanModel>> CreateFacilityReportingPlanAsync(
        FacilityReportingPlanModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityReportingPlanModel>(() => Request("/dmrp/facility-reporting-plans")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityReportingPlanModel>> GetFacilityReportingPlanAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityReportingPlanModel>(() => Request($"/dmrp/facility-reporting-plans/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityReportingPlanModel>> UpdateFacilityReportingPlanAsync(
        string id,
        FacilityReportingPlanModel request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityReportingPlanModel>(() => Request($"/dmrp/facility-reporting-plans/{id}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityReportingPlanAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/dmrp/facility-reporting-plans/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> SearchFacilityReportingPlansAsync(
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("/dmrp/facility-reporting-plans")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));
}
