using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueriedFhirResourceRepository : MongoDbRepository<QueriedFhirResourceRecord>, IQueriedFhirResourceRepository
{
    public QueriedFhirResourceRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoDbRepository<QueriedFhirResourceRecord>> logger = null) : base(mongoSettings, logger)
    {
    }

    public async Task<List<QueriedFhirResourceRecord>> GetQueryResultsAsync(string facilityId, string? patientId = default, string? correlationId = default, CancellationToken cancellationToken = default)
    {
        var filter = Builders<QueriedFhirResourceRecord>.Filter.Eq(x => x.FacilityId, facilityId);

        if(!string.IsNullOrWhiteSpace(patientId))
        {
            filter = filter & Builders<QueriedFhirResourceRecord>.Filter.Eq(x => x.PatientId, patientId);
        }

        if(!string.IsNullOrWhiteSpace(correlationId))
        {
            filter = filter & Builders<QueriedFhirResourceRecord>.Filter.Eq(x => x.CorrelationId, correlationId);
        }

        var queryResult = await _collection.Find(filter).ToListAsync();

        return queryResult;
    }
}
