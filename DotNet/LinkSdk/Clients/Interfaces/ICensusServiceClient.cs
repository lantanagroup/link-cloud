using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Census;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.Sdk.Clients;

public interface ICensusServiceClient
{
    Task<CensusConfigApiModel> CreateCensusConfigAsync(CensusConfigApiModel request, CancellationToken cancellationToken = default);
    Task<CensusConfigApiModel?> GetCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<CensusConfigApiModel> UpdateCensusConfigAsync(string facilityId, CensusConfigApiModel request, CancellationToken cancellationToken = default);
    Task DeleteCensusConfigAsync(string facilityId, CancellationToken cancellationToken = default);
    Task<CensusFhirListApiModel?> GetAdmittedPatientsAsync(string facilityId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<CensusPatientEncounterApiModel>> GetCurrentPatientEncountersAsync(string facilityId, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<CensusPatientEncounterApiModel>> GetHistoricalPatientEncountersAsync(string facilityId, DateTime dateThreshold, string? correlationId = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task RebuildPatientEncountersAsync(string facilityId, string? correlationId = null, CancellationToken cancellationToken = default);
    Task<PagedConfigModel<CensusPatientEventApiModel>> GetPatientEventsAsync(string facilityId, string? correlationId = null, DateTime? startDate = null, DateTime? endDate = null, string? sortBy = null, SortOrder? sortOrder = null, int pageSize = 10, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task DeletePatientEventAsync(string id, CancellationToken cancellationToken = default);
    Task DeletePatientEventsByCorrelationAsync(string correlationId, CancellationToken cancellationToken = default);
}
