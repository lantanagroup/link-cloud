using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LinqKit;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryManager
{
    Task<FhirQueryResultModel> GetFhirQueriesAsync(string facilityId, string? correlationId = default, string? patientId = default, string? resourceType = default, CancellationToken cancellationToken = default);
    Task<FhirQuery> AddAsync(FhirQuery entity, CancellationToken cancellationToken = default);
}
public class FhirQueryManager : IFhirQueryManager
{
    private readonly ILogger<FhirQueryManager> _logger;
    private readonly IDatabase _database;

    public FhirQueryManager(ILogger<FhirQueryManager> logger, IDatabase database)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<FhirQuery> AddAsync(FhirQuery entity, CancellationToken cancellationToken = default)
    {
        entity.Id = Guid.NewGuid().ToString();
        entity.CreateDate = DateTime.UtcNow;
        entity.ModifyDate = DateTime.UtcNow;

        await _database.FhirQueryRepository.AddAsync(entity);
        await _database.FhirQueryRepository.SaveChangesAsync();

        return entity;
    }

    public async Task<FhirQueryResultModel> GetFhirQueriesAsync(string facilityId, string? correlationId = null, string? patientId = null, string? resourceType = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentNullException(nameof(facilityId));
        }

        Expression<Func<FhirQuery, bool>> predicate = PredicateBuilder.New<FhirQuery>(x => x.FacilityId == facilityId);

        if (!string.IsNullOrEmpty(correlationId))
        {
            predicate = predicate.And(x => x.DataAcquisitionLog.CorrelationId == correlationId);
        }

        if (!string.IsNullOrEmpty(patientId))
        {
            predicate = predicate.And(x => x.DataAcquisitionLog.PatientId == patientId);
        }

        if (!string.IsNullOrEmpty(resourceType))
        {
            predicate = predicate.And(x => x.DataAcquisitionLog.FhirQuery.Any(y => y.ResourceTypes.Any(z => z.Equals(resourceType))));
        }

        return new FhirQueryResultModel { Queries = (await _database.FhirQueryRepository.FindAsync(predicate)).ToList() };
    }
}
