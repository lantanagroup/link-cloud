using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Sdk.ApiClient;
using System.Net;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class CensusServiceClient : LinkApiClientBase
{
    public CensusServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public async Task<(HttpStatusCode StatusCode, CensusConfigApiModel? Response)> CreateCensusConfigAsync(
        CensusConfigApiModel request,
        CancellationToken cancellationToken = default)
    {
        var response = await Request("census/config")
            .AllowAnyHttpStatus()
            .PostJsonAsync(request, cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<CensusConfigApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, CensusConfigApiModel? Response)> GetCensusConfigAsync(
        string facilityId,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"census/config/{facilityId}")
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<CensusConfigApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, CensusConfigApiModel? Response)> UpdateCensusConfigAsync(
        string facilityId,
        CensusConfigApiModel request,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"census/config/{facilityId}")
            .AllowAnyHttpStatus()
            .PutJsonAsync(request, cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<CensusConfigApiModel>(response));
    }

    public async Task<HttpStatusCode> DeleteCensusConfigAsync(
        string facilityId,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"census/config/{facilityId}")
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<(HttpStatusCode StatusCode, CensusFhirListApiModel? Response)> GetAdmittedPatientsAsync(
        string facilityId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"census/{facilityId}/history/admitted")
            .SetQueryParam("startDate", startDate)
            .SetQueryParam("endDate", endDate)
            .AllowAnyHttpStatus()
            .GetAsync(cancellationToken: cancellationToken);

        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<CensusFhirListApiModel>(response));
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<CensusPatientEncounterApiModel>? Response)> GetCurrentPatientEncountersAsync(
        string facilityId,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var request = Request("census/patient-encounters/current")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .AllowAnyHttpStatus();

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        var response = await request.GetAsync(cancellationToken: cancellationToken);
        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<CensusPatientEncounterApiModel>>(response));
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<CensusPatientEncounterApiModel>? Response)> GetHistoricalPatientEncountersAsync(
        string facilityId,
        DateTime dateThreshold,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var request = Request("census/patient-encounters/historical")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("dateThreshold", dateThreshold)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .AllowAnyHttpStatus();

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        var response = await request.GetAsync(cancellationToken: cancellationToken);
        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<CensusPatientEncounterApiModel>>(response));
    }

    public async Task<HttpStatusCode> RebuildPatientEncountersAsync(
        string facilityId,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var request = Request("census/patient-encounters/rebuild")
            .SetQueryParam("facilityId", facilityId)
            .AllowAnyHttpStatus();

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);

        var response = await request.PostAsync(cancellationToken: cancellationToken);
        return response.ResponseMessage.StatusCode;
    }

    public async Task<(HttpStatusCode StatusCode, PagedConfigModel<CensusPatientEventApiModel>? Response)> GetPatientEventsAsync(
        string facilityId,
        string? correlationId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var request = Request("census/patient-events")
            .SetQueryParam("facilityId", facilityId)
            .SetQueryParam("pageSize", pageSize)
            .SetQueryParam("pageNumber", pageNumber)
            .AllowAnyHttpStatus();

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);
        if (startDate.HasValue) request = request.SetQueryParam("startDate", startDate.Value);
        if (endDate.HasValue) request = request.SetQueryParam("endDate", endDate.Value);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        var response = await request.GetAsync(cancellationToken: cancellationToken);
        return (response.ResponseMessage.StatusCode, await ReadJsonAsync<PagedConfigModel<CensusPatientEventApiModel>>(response));
    }

    public async Task<HttpStatusCode> DeletePatientEventAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"census/patient-events/{id}")
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }

    public async Task<HttpStatusCode> DeletePatientEventsByCorrelationAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var response = await Request($"census/patient-events/visit/{correlationId}")
            .AllowAnyHttpStatus()
            .DeleteAsync(cancellationToken: cancellationToken);

        return response.ResponseMessage.StatusCode;
    }
}
