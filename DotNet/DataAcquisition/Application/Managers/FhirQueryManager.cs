using DataAcquisition.Domain;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LinqKit;
using System.Linq.Expressions;

namespace LantanaGroup.Link.DataAcquisition.Application.Managers;

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

        await _database.FhirQueryRepository.AddAsync(entity, cancellationToken);

        return entity;
    }

    public async Task<FhirQueryResultModel> GetFhirQueriesAsync(string facilityId, string? correlationId = null, string? patientId = null, string? resourceType = null, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentNullException(nameof(facilityId));
        }

        Expression<Func<FhirQuery, bool>> predicate = PredicateBuilder.New<FhirQuery>(x => x.FacilityId == facilityId);

        if (!string.IsNullOrEmpty(correlationId))
        {
            predicate = predicate.And(x => x.CorrelationId == correlationId);
        }

        if (!string.IsNullOrEmpty(patientId))
        {
            predicate = predicate.And(x => x.PatientId == patientId);
        }

        if (!string.IsNullOrEmpty(resourceType))
        {
            predicate = predicate.And(x => x.ResourceType == resourceType);
        }

        return new FhirQueryResultModel { Queries = (await _database.FhirQueryRepository.FindAsync(predicate, cancellationToken)).ToList() };
    }
}
