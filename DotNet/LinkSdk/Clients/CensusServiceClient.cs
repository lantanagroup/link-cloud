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

    public Task<CensusConfigApiModel> CreateCensusConfigAsync(CensusConfigApiModel request, CancellationToken cancellationToken = default) =>
        Request("census/config").PostJsonAsync(request, cancellationToken: cancellationToken).ReceiveJson<CensusConfigApiModel>();

    public Task<CensusConfigApiModel?> GetCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"census/config/{facilityId}").GetJsonAsync<CensusConfigApiModel>(cancellationToken: cancellationToken));

    public Task<CensusConfigApiModel> UpdateCensusConfigAsync(string facilityId, CensusConfigApiModel request, CancellationToken cancellationToken = default) =>
        Request($"census/config/{facilityId}").PutJsonAsync(request, cancellationToken: cancellationToken).ReceiveJson<CensusConfigApiModel>();

    public Task DeleteCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"census/config/{facilityId}").DeleteAsync(cancellationToken: cancellationToken));

    public Task<CensusFhirListApiModel?> GetAdmittedPatientsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync(() => Request($"census/{facilityId}/history/admitted").SetQueryParam("startDate", startDate).SetQueryParam("endDate", endDate).GetJsonAsync<CensusFhirListApiModel>(cancellationToken: cancellationToken));

    public Task<PagedConfigModel<CensusPatientEncounterApiModel>> GetCurrentPatientEncountersAsync(string facilityId, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-encounters/current").SetQueryParam("facilityId", facilityId).SetQueryParam("pageSize", pageSize).SetQueryParam("pageNumber", pageNumber);
        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) r = r.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) r = r.SetQueryParam("sortOrder", sortOrder.Value.ToString());
        return r.GetJsonAsync<PagedConfigModel<CensusPatientEncounterApiModel>>(cancellationToken: cancellationToken);
    }

    public Task<PagedConfigModel<CensusPatientEncounterApiModel>> GetHistoricalPatientEncountersAsync(string facilityId, DateTime dateThreshold, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-encounters/historical").SetQueryParam("facilityId", facilityId).SetQueryParam("dateThreshold", dateThreshold).SetQueryParam("pageSize", pageSize).SetQueryParam("pageNumber", pageNumber);
        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) r = r.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) r = r.SetQueryParam("sortOrder", sortOrder.Value.ToString());
        return r.GetJsonAsync<PagedConfigModel<CensusPatientEncounterApiModel>>(cancellationToken: cancellationToken);
    }

    public Task RebuildPatientEncountersAsync(string facilityId, string? correlationId = null, CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-encounters/rebuild").SetQueryParam("facilityId", facilityId);
        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        return r.PostAsync(cancellationToken: cancellationToken);
    }

    public async Task<PagedConfigModel<CensusPatientEventApiModel>> GetPatientEventsAsync(string facilityId, string? correlationId = null, DateTime? startDate = null, DateTime? endDate = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var r = Request("census/patient-events").SetQueryParam("facilityId", facilityId).SetQueryParam("pageSize", pageSize).SetQueryParam("pageNumber", pageNumber);
        if (!string.IsNullOrWhiteSpace(correlationId)) r = r.SetQueryParam("correlationId", correlationId);
        if (startDate.HasValue) r = r.SetQueryParam("startDate", startDate.Value);
        if (endDate.HasValue) r = r.SetQueryParam("endDate", endDate.Value);
        if (!string.IsNullOrWhiteSpace(sortBy)) r = r.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) r = r.SetQueryParam("sortOrder", sortOrder.Value.ToString());
        return await GetOrDefaultAsync(() => r.GetJsonAsync<PagedConfigModel<CensusPatientEventApiModel>>(cancellationToken: cancellationToken)) ?? new PagedConfigModel<CensusPatientEventApiModel>();
    }

    public Task DeletePatientEventAsync(string id, CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"census/patient-events/{id}").DeleteAsync(cancellationToken: cancellationToken));

    public Task DeletePatientEventsByCorrelationAsync(string correlationId, CancellationToken cancellationToken = default) =>
        DeleteOrIgnoreAsync(() => Request($"census/patient-events/visit/{correlationId}").DeleteAsync(cancellationToken: cancellationToken));
}
