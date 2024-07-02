using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces;

public interface IQueriedFhirResourceRepository : IEntityRepository<QueriedFhirResourceRecord>, IDisposable
{
    Task<List<QueriedFhirResourceRecord>> GetQueryResultsAsync(string correlationId, string queryType, bool successOnly);
}
