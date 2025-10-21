using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LinqKit;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

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
            ResourceTypes = model.ResourceTypes,
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
        var entity = await _database.FhirQueryRepository.SingleOrDefaultAsync(q => q.Id == model.Id && q.FacilityId == model.FacilityId);

        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        entity.ResourceReferenceTypes = (await _database.ResourceReferenceTypeRepository.FindAsync(r => r.FhirQueryId == entity.Id)).ToList();

        entity.ResourceReferenceTypes.ForEach(_database.ResourceReferenceTypeRepository.Remove);
        entity.ResourceReferenceTypes.Clear();

        entity.QueryParameters = model.QueryParameters;
        entity.IdQueryParameterValues = model.IdQueryParameterValues;
        entity.MeasureId = model.MeasureId;
        entity.IsReference = model.IsReference;
        entity.ResourceReferenceTypes = model.ResourceReferenceTypes.Select(ResourceReferenceTypeModel.ToDomain).ToList();
        entity.QueryType = model.QueryType;
        entity.ResourceTypes = model.ResourceTypes;
        entity.Paged = model.Paged;
        entity.DataAcquisitionLogId = model.DataAcquisitionLogId;
        entity.ModifyDate = DateTime.UtcNow;

        await _database.FhirQueryRepository.SaveChangesAsync();
        return entity;
    }
}
