using DataAcquisition.Domain.Application.Models;
using DnsClient.Protocol;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LinqKit;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Results;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryManager
{
    Task<FhirQuery> CreateAsync(CreateFhirQueryModel entity, CancellationToken cancellationToken = default);
    Task<FhirQuery> UpdateAsync(FhirQueryModel entity, CancellationToken cancellationToken = default);
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

    public async Task<FhirQuery> CreateAsync(CreateFhirQueryModel model, CancellationToken cancellationToken = default)
    {
        if(string.IsNullOrEmpty(model.FacilityId))
        {
            throw new ArgumentNullException("FacilityId cannot be null");
        }

        var entity = new FhirQuery()
        {
            CreateDate = DateTime.UtcNow,
            ModifyDate = DateTime.UtcNow,
            QueryParameters = model.QueryParameters,
            IsReference = model.IsReference,
            DataAcquisitionLogId = model.DataAcquisitionLogId,
            FacilityId = model.FacilityId,
            FhirQueryResourceTypes = model.ResourceTypes.Select(r => new FhirQueryResourceType
            {
                ResourceType = r
            }).ToList(),
            ResourceReferenceTypes = model.ResourceReferenceTypes.Select(r => new ResourceReferenceType
            {
                FacilityId = model.FacilityId,
                QueryPhase = r.QueryPhase,
                ResourceType = r.ResourceType,
            }).ToList(),
            MeasureId = model.MeasureId,
            Paged = model.Paged,
            QueryType = model.QueryType
        };

        await _database.FhirQueryRepository.AddAsync(entity);
        await _database.ResourceReferenceTypeRepository.AddRangeAsync(entity.ResourceReferenceTypes);

        entity.ResourceReferenceTypes.ForEach(r => r.FhirQueryId = entity.Id);

        await _database.FhirQueryRepository.SaveChangesAsync();

        return entity;
    }

    public async Task<FhirQuery> UpdateAsync(FhirQueryModel model, CancellationToken cancellationToken = default)
    {
        var query = await _database.FhirQueryRepository.SingleOrDefaultAsync(q => q.Id == model.Id && q.FacilityId == model.FacilityId);

        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        query.ResourceReferenceTypes = (await _database.ResourceReferenceTypeRepository.FindAsync(r => r.FhirQueryId == query.Id)).ToList();
        query.ResourceReferenceTypes.ForEach(_database.ResourceReferenceTypeRepository.Remove);
        query.ResourceReferenceTypes.Clear();

        query.FhirQueryResourceTypes = (await _database.FhirQueryResourceTypeRepository.FindAsync(r => r.FhirQueryId == query.Id)).ToList();
        query.FhirQueryResourceTypes.ForEach(_database.FhirQueryResourceTypeRepository.Remove);
        query.FhirQueryResourceTypes.Clear();

        query.QueryParameters = model.QueryParameters;
        query.IdQueryParameterValues = model.IdQueryParameterValues;
        query.MeasureId = model.MeasureId;
        query.IsReference = model.IsReference;
        query.ResourceReferenceTypes = model.ResourceReferenceTypes.Select(r => new ResourceReferenceType
        {
            FacilityId = r.FacilityId,
            FhirQueryId = query.Id,
            QueryPhase = r.QueryPhase,
            ResourceType = r.ResourceType,
            CreateDate = DateTime.UtcNow
        }).ToList();
        query.QueryType = model.QueryType;

        query.FhirQueryResourceTypes = model.ResourceTypes.Select(r => new FhirQueryResourceType
        {
            ResourceType = r
        }).ToList();

        query.Paged = model.Paged;
        query.DataAcquisitionLogId = model.DataAcquisitionLogId;
        query.ModifyDate = DateTime.UtcNow;

        await _database.FhirQueryRepository.SaveChangesAsync();
        return query;
    }
}
