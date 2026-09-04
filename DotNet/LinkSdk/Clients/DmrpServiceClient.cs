using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class DmrpServiceClient : LinkApiClientBase, IDmrpServiceClient
{
    public DmrpServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        // DMRP is a module hosted by the Tenant service rather than a service of its own, so its
        // endpoints are served from the Tenant service's base address.
        : base(
            serviceRegistry.Value.TenantServiceApiUrl
                ?? throw new InvalidOperationException("Tenant service URL is not configured in ServiceRegistry."),
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

    public Task<LinkApiResponse<PagedConfigModel<MeasureMappingModel>>> SearchMeasureMappingsAsync(
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SearchMeasureMappingsAsync(measure: null, dqm: null, frequency: null, pageSize, pageNumber,
            cancellationToken);

    public Task<LinkApiResponse<PagedConfigModel<MeasureMappingModel>>> SearchMeasureMappingsAsync(
        string? measure,
        string? dqm = null,
        Frequency? frequency = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        // The searchable listing is /search; the collection route itself only accepts POST. Reading
        // measure-mappings without the segment answers 404, which reads as "DMRP is switched off".
        SendAsync<PagedConfigModel<MeasureMappingModel>>(() => Request("/dmrp/measure-mappings/search")
            .SetQueryParam("measure", measure)
            .SetQueryParam("dqm", dqm)
            .SetQueryParam("frequency", frequency)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityReportingPlanModel>> CreateFacilityReportingPlanAsync(
        FacilityReportingPlanRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityReportingPlanModel>(() => Request("/dmrp/reporting-plans")
            .PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityReportingPlanModel>> GetFacilityReportingPlanAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityReportingPlanModel>(() => Request($"/dmrp/reporting-plans/{id}")
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<FacilityReportingPlanModel>> UpdateFacilityReportingPlanAsync(
        string id,
        FacilityReportingPlanUpdateRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<FacilityReportingPlanModel>(() => Request($"/dmrp/reporting-plans/{id}")
            .PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityReportingPlanAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/dmrp/reporting-plans/{id}")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanModel>>> SearchFacilityReportingPlansAsync(
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<FacilityReportingPlanModel>>(() => Request("/dmrp/reporting-plans")
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanPeriodModel>>> GetFacilityReportingPlanPeriodsAsync(
        string facilityId,
        int? monthsAhead = null,
        bool? isReporting = null,
        bool refresh = false,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<FacilityReportingPlanPeriodModel>>(
            () => Request($"/dmrp/reporting-plans/facilities/{facilityId}/periods")
                .SetQueryParam("monthsAhead", monthsAhead)
                .SetQueryParam("isReporting", isReporting)
                // Sent only when asked for, so a default call cannot be mistaken for one that
                // deliberately declined a refresh.
                .SetQueryParam("refresh", refresh ? "true" : null)
                .SetQueryParam("pageSize", pageSize)
                .SetQueryParam("pageNumber", pageNumber)
                .GetAsync(cancellationToken: cancellationToken));

    public async Task<LinkApiResponse<List<FacilityReportingPlanModel>>> GetFacilityReportingPlansForFacilityAsync(
        string facilityId,
        int? month = null,
        int? year = null,
        bool? isReporting = null,
        int? monthsAhead = null,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<List<FacilityReportingPlanModel>>(
            () => Request($"/dmrp/reporting-plans/facilities/{facilityId}")
                .SetQueryParam("month", month)
                .SetQueryParam("year", year)
                .SetQueryParam("isReporting", isReporting)
                .SetQueryParam("monthsAhead", monthsAhead)
                .SetQueryParam("refresh", refresh ? "true" : null)
                .GetAsync(cancellationToken: cancellationToken));

        if (response.IsSuccessStatusCode && response.Body is null)
        {
            return new LinkApiResponse<List<FacilityReportingPlanModel>>
            {
                StatusCode = response.StatusCode,
                Body = [],
                RawBody = response.RawBody,
                ContentType = response.ContentType,
                RequestUrl = response.RequestUrl,
                RequestMethod = response.RequestMethod,
                RequestBody = response.RequestBody,
                TraceId = response.TraceId
            };
        }

        return response;
    }

    public Task<LinkApiResponse<PagedConfigModel<FacilityReportingPlanModel>>> SearchFacilityReportingPlansAsync(
        string? facilityId,
        string? measureMappingId = null,
        int? month = null,
        int? year = null,
        bool? isReporting = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default) =>
        SendAsync<PagedConfigModel<FacilityReportingPlanModel>>(() => Request("/dmrp/reporting-plans/search")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("measureMappingId", measureMappingId)
            .SetQueryParam("month", month)
            .SetQueryParam("year", year)
            .SetQueryParam("isReporting", isReporting)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityReportingPlansAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("/dmrp/reporting-plans")
            .DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteFacilityReportingPlansForFacilityAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"/dmrp/reporting-plans/facilities/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken));
}
