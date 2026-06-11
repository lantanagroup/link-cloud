using Flurl.Http;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Sdk.Clients;

public class CensusServiceClient : LinkApiClientBase, ICensusServiceClient
{
    public CensusServiceClient(
        IOptions<ServiceRegistry> serviceRegistry,
        IOptions<BackendAuthenticationServiceExtension.LinkBearerServiceOptions> bearerOptions,
        IOptions<LinkTokenServiceSettings> tokenServiceSettings,
        ICreateSystemToken tokenService)
        : base(
            serviceRegistry.Value.CensusServiceApiUrl
                ?? throw new InvalidOperationException("Census service URL is not configured in ServiceRegistry."),
            bearerOptions, tokenServiceSettings, tokenService)
    { }

    public Task<LinkApiResponse<CensusConfigApiModel>> CreateCensusConfigAsync(CensusConfigApiModel request, CancellationToken cancellationToken = default) =>
        SendAsync<CensusConfigApiModel>(() => Request("census/config").PostJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse<CensusConfigApiModel>> GetCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync<CensusConfigApiModel>(() => Request($"census/config/{facilityId}").GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse<CensusConfigApiModel>> UpdateCensusConfigAsync(string facilityId, CensusConfigApiModel request, CancellationToken cancellationToken = default) =>
        SendAsync<CensusConfigApiModel>(() => Request($"census/config/{facilityId}").PutJsonAsync(request, cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeleteCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"census/config/{facilityId}").DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DisableFacilityJobsAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"census/config/{facilityId}/jobs").DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> EnableFacilityJobsAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"census/config/{facilityId}/jobs/restore").PatchAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetAdmittedPatientsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"census/{facilityId}/history/admitted").SetQueryParam("startDate", startDate).SetQueryParam("endDate", endDate).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> GetCurrentPatientEncountersAsync(
        string facilityId,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-encounters/current")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber);

        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) r = r.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) r = r.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        return SendAsync(() => r.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> GetCurrentPatientEncountersAsync(string facilityId, CancellationToken cancellationToken = default) =>
        GetCurrentPatientEncountersAsync(
            facilityId,
            correlationId: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: cancellationToken);

    public Task<LinkApiResponse> GetHistoricalPatientEncountersAsync(
        string facilityId,
        DateTime? dateThreshold,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-encounters/historical")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber);

        if (dateThreshold.HasValue) r = r.SetQueryParam("dateThreshold", dateThreshold.Value);

        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) r = r.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) r = r.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        return SendAsync(() => r.GetAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> GetHistoricalPatientEncountersAsync(string facilityId, DateTime? dateThreshold, CancellationToken cancellationToken = default) =>
        GetHistoricalPatientEncountersAsync(
            facilityId,
            dateThreshold,
            correlationId: null,
            sortBy: null,
            sortOrder: null,
            pageSize: 10,
            pageNumber: 1,
            cancellationToken: cancellationToken);

    public Task<LinkApiResponse> RebuildPatientEncountersAsync(string facilityId, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-encounters/rebuild").SetQueryParam("facilityId", facilityId);
        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        return SendAsync(() => r.PostAsync(cancellationToken: cancellationToken));
    }

    public Task<LinkApiResponse> GetPatientEventsAsync(string facilityId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request("census/patient-events").SetQueryParam("facilityId", facilityId).GetAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeletePatientEventAsync(string id, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"census/patient-events/{id}").DeleteAsync(cancellationToken: cancellationToken));

    public Task<LinkApiResponse> DeletePatientEventsByCorrelationAsync(string correlationId, CancellationToken cancellationToken = default) =>
        SendAsync(() => Request($"census/patient-events/visit/{correlationId}").DeleteAsync(cancellationToken: cancellationToken));
}
