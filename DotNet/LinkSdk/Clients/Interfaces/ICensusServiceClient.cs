using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;

namespace LantanaGroup.Link.Sdk.Clients;

public interface ICensusServiceClient
{
    Task<LinkApiResponse<CensusConfigApiModel>> CreateCensusConfigAsync(CensusConfigApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<CensusConfigApiModel>> GetCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse<CensusConfigApiModel>> UpdateCensusConfigAsync(string facilityId, CensusConfigApiModel request, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeleteCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DisableFacilityJobsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> EnableFacilityJobsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetAdmittedPatientsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetCurrentPatientEncountersAsync(
        string facilityId,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetCurrentPatientEncountersAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetHistoricalPatientEncountersAsync(
        string facilityId,
        DateTime? dateThreshold,
        string? correlationId = null,
        string? sortBy = null,
        SortOrder? sortOrder = null,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetHistoricalPatientEncountersAsync(string facilityId, DateTime? dateThreshold, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> RebuildPatientEncountersAsync(string facilityId, string? correlationId = null, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> GetPatientEventsAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeletePatientEventAsync(string id, CancellationToken cancellationToken = default);
    Task<LinkApiResponse> DeletePatientEventsByCorrelationAsync(string correlationId, CancellationToken cancellationToken = default);
}
