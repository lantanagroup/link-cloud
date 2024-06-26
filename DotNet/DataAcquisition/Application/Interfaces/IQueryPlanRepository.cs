using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IQueryPlanRepository : IPersistenceRepository<QueryPlan>, IDisposable
{
    Task<List<QueryPlan>> GetQueryPlansByFacilityId(string facilityId, CancellationToken cancellationToken = default);
    Task<QueryPlan?> GetByFacilityAndReportAsync(string facilityId, string reportType, CancellationToken cancellationToken = default);
    Task DeleteSupplementalQueriesForFacility(string facilityId, CancellationToken cancellationToken = default);
    Task DeleteInitialQueriesForFacility(string facilityId, CancellationToken cancellationToken = default);
    Task SaveInitialQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken);
    Task SaveSupplementalQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken);
}
