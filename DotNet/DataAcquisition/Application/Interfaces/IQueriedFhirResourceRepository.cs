using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IQueriedFhirResourceRepository : IMongoDbRepository<QueriedFhirResourceRecord>, IDisposable
{
    Task<List<QueriedFhirResourceRecord>> GetQueryResultsAsync(string facilityId, string? patientId = default, string? correlationId = default, CancellationToken cancellationToken = default);
}
