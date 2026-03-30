using Flurl.Http;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Sdk.ApiClient;

namespace LantanaGroup.Link.Sdk.Clients;

public sealed class CensusServiceClient : LinkApiClientBase
{
    public CensusServiceClient(ApiClientSettings settings)
        : base(settings)
    {
    }

    public Task<CensusConfigApiModel> CreateCensusConfigAsync(
        CensusConfigApiModel request,
        CancellationToken cancellationToken = default) =>
        Request("census/config")
            .PostJsonAsync(request, cancellationToken: cancellationToken)
            .ReceiveJson<CensusConfigApiModel>();

    public Task<CensusConfigApiModel> GetCensusConfigAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"census/config/{facilityId}")
            .GetJsonAsync<CensusConfigApiModel>(cancellationToken: cancellationToken);

    public Task<CensusConfigApiModel> UpdateCensusConfigAsync(
        string facilityId,
        CensusConfigApiModel request,
        CancellationToken cancellationToken = default) =>
        Request($"census/config/{facilityId}")
            .PutJsonAsync(request, cancellationToken: cancellationToken)
            .ReceiveJson<CensusConfigApiModel>();

    public Task DeleteCensusConfigAsync(
        string facilityId,
        CancellationToken cancellationToken = default) =>
        Request($"census/config/{facilityId}")
            .DeleteAsync(cancellationToken: cancellationToken);

    public Task<CensusFhirListApiModel> GetAdmittedPatientsAsync(
        string facilityId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default) =>
        Request($"census/{facilityId}/history/admitted")
            .SetQueryParam("startDate", startDate)
            .SetQueryParam("endDate", endDate)
            .GetJsonAsync<CensusFhirListApiModel>(cancellationToken: cancellationToken);

    public Task<PagedConfigModel<CensusPatientEncounterApiModel>> GetCurrentPatientEncountersAsync(
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
            .SetQueryParam("pageNumber", pageNumber);

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        return request.GetJsonAsync<PagedConfigModel<CensusPatientEncounterApiModel>>(cancellationToken: cancellationToken);
    }

    public Task<PagedConfigModel<CensusPatientEncounterApiModel>> GetHistoricalPatientEncountersAsync(
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
            .SetQueryParam("pageNumber", pageNumber);

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        return request.GetJsonAsync<PagedConfigModel<CensusPatientEncounterApiModel>>(cancellationToken: cancellationToken);
    }

    public Task RebuildPatientEncountersAsync(
        string facilityId,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var request = Request("census/patient-encounters/rebuild")
            .SetQueryParam("facilityId", facilityId);

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);

        return request.PostAsync(cancellationToken: cancellationToken);
    }

    public Task<PagedConfigModel<CensusPatientEventApiModel>> GetPatientEventsAsync(
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
            .SetQueryParam("pageNumber", pageNumber);

        if (!string.IsNullOrWhiteSpace(correlationId)) request = request.SetQueryParam("correlationId", correlationId);
        if (startDate.HasValue) request = request.SetQueryParam("startDate", startDate.Value);
        if (endDate.HasValue) request = request.SetQueryParam("endDate", endDate.Value);
        if (!string.IsNullOrWhiteSpace(sortBy)) request = request.SetQueryParam("sortBy", sortBy);
        if (sortOrder.HasValue) request = request.SetQueryParam("sortOrder", sortOrder.Value.ToString());

        return request.GetJsonAsync<PagedConfigModel<CensusPatientEventApiModel>>(cancellationToken: cancellationToken);
    }

    public Task DeletePatientEventAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        Request($"census/patient-events/{id}")
            .DeleteAsync(cancellationToken: cancellationToken);

    public Task DeletePatientEventsByCorrelationAsync(
        string correlationId,
        CancellationToken cancellationToken = default) =>
        Request($"census/patient-events/visit/{correlationId}")
            .DeleteAsync(cancellationToken: cancellationToken);
}
