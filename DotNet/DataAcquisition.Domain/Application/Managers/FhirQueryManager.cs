using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;

public interface IFhirQueryManager
{
    Task<FhirQuery> CreateAsync(CreateFhirQueryModel entity, CancellationToken cancellationToken = default);
    Task<FhirQuery> UpdateAsync(FhirQueryModel entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Targeted update of only the QueryParameters column for a FhirQuery.
    /// Uses ExecuteUpdateAsync to issue a single UPDATE without loading the entity,
    /// minimising lock duration under high concurrency.
    /// </summary>
    Task UpdateQueryParametersAsync(Guid fhirQueryId, List<string> queryParameters, CancellationToken cancellationToken = default);
}
public class FhirQueryManager : IFhirQueryManager
{
    private readonly ILogger<FhirQueryManager> _logger;
    private readonly IDatabase _database;
    private readonly DataAcquisitionDbContext _dbContext;

    public FhirQueryManager(ILogger<FhirQueryManager> logger, IDatabase database, DataAcquisitionDbContext dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<FhirQuery> CreateAsync(CreateFhirQueryModel model, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirQueryManager.CreateAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);

        if (string.IsNullOrEmpty(model.FacilityId))
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
            QueryType = model.QueryType,
        };

        await _database.FhirQueryRepository.AddAsync(entity);
        await _database.ResourceReferenceTypeRepository.AddRangeAsync(entity.ResourceReferenceTypes);

        entity.ResourceReferenceTypes.ForEach(r => r.FhirQueryId = entity.Id);

        await _database.FhirQueryRepository.SaveChangesAsync();

        return entity;
    }

    public async Task UpdateQueryParametersAsync(Guid fhirQueryId, List<string> queryParameters, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirQueryManager.UpdateQueryParametersAsync");

        var updated = await _dbContext.FhirQueries
            .Where(q => q.Id == fhirQueryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(q => q.QueryParameters, queryParameters)
                .SetProperty(q => q.ModifyDate, DateTime.UtcNow),
            cancellationToken);

        if (updated == 0)
            throw new InvalidOperationException($"FhirQuery with ID {fhirQueryId} not found.");
    }

    public async Task<FhirQuery> UpdateAsync(FhirQueryModel model, CancellationToken cancellationToken = default)
    {
        using var activity = ServiceActivitySource.Instance.StartActivity("FhirQueryManager.UpdateAsync");
        activity?.SetTag(DiagnosticNames.FacilityId, model.FacilityId);

        var query = await _database.FhirQueryRepository.SingleOrDefaultAsync(
            q => q.Id == model.Id && q.FacilityId == model.FacilityId,
            cancellationToken);

        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        // Scalar fields: update only if provided (non-null / non-default)
        if (model.QueryParameters?.Any() == true)
            query.QueryParameters = model.QueryParameters.ToList();

        if (model.IdQueryParameterValues?.Any() == true)
            query.IdQueryParameterValues = model.IdQueryParameterValues.ToList();

        if (model.MeasureId != null)
            query.MeasureId = model.MeasureId;

        if (model.IsReference.HasValue)
            query.IsReference = model.IsReference.Value;

        query.QueryType = model.QueryType;

        if (model.Paged.HasValue)
            query.Paged = model.Paged.Value;

        if (model.DataAcquisitionLogId != default)
            query.DataAcquisitionLogId = model.DataAcquisitionLogId;

        // Collections: only replace if caller explicitly provides a non-empty list
        // Otherwise leave untouched (critical for partial updates like reference collection)
        if (model.ResourceReferenceTypes?.Any() == true)
        {
            // If you want to allow full replacement when explicitly sent
            query.ResourceReferenceTypes.Clear();
            foreach (var r in model.ResourceReferenceTypes)
            {
                query.ResourceReferenceTypes.Add(new ResourceReferenceType
                {
                    FacilityId = r.FacilityId,
                    FhirQueryId = query.Id,
                    QueryPhase = r.QueryPhase,
                    ResourceType = r.ResourceType,
                    CreateDate = DateTime.UtcNow
                });
            }
        }

        if (model.ResourceTypes?.Any() == true)
        {
            query.FhirQueryResourceTypes.Clear();
            foreach (var r in model.ResourceTypes)
            {
                query.FhirQueryResourceTypes.Add(new FhirQueryResourceType
                {
                    FhirQueryId = query.Id,
                    ResourceType = r
                });
            }
        }

        query.ModifyDate = DateTime.UtcNow;

        await _database.FhirQueryRepository.SaveChangesAsync(cancellationToken);
        return query;
    }
}
