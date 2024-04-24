using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class QueryPlanRepository : MongoDbRepository<QueryPlan>, IQueryPlanRepository
{
    public QueryPlanRepository(IOptions<MongoConnection> mongoSettings) : base(mongoSettings)
    {
    }

    public override async Task<QueryPlan> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await(await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return queryResult;
    }

    public async Task<List<QueryPlan>> GetQueryPlansByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await (await _collection.FindAsync(filter)).ToListAsync();
        return queryResult;
    }

    public async Task<QueryPlan> GetByFacilityAndReportAsync(string facilityId, string reportType, CancellationToken cancellationToken = default)
    {
        var filter = (Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId) & Builders<QueryPlan>.Filter.Eq(x => x.ReportType, reportType));
        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return queryResult;
    }

    public override async Task<QueryPlan> UpdateAsync(QueryPlan Entity, CancellationToken cancellationToken = default)
    {
        var existingQueryPlan = await GetAsync(Entity.FacilityId, cancellationToken);

        if(existingQueryPlan != null)
        {
            Entity.Id = existingQueryPlan.Id;
            Entity.CreateDate = existingQueryPlan.CreateDate;
        }
        else
        {
            Entity.Id = Guid.NewGuid().ToString();
            Entity.CreateDate = DateTime.UtcNow;
        }

        Entity.ModifyDate = DateTime.UtcNow;

        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, Entity.FacilityId);
        var result = await _collection.ReplaceOneAsync(filter, Entity, new ReplaceOptions { IsUpsert = true });

        try
        {
            if (result.UpsertedId != null && string.IsNullOrWhiteSpace(Entity.Id))
            {
                Entity.Id = result.UpsertedId.ToString();
            }
        }
        catch (Exception ex)
        {
            //just returning the entity. Getting upsertedId can cause an exception.
        }

        return Entity;
    }

    public override async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);
        await _collection.DeleteOneAsync(filter);
    }

    public async Task DeleteSupplementalQueriesForFacility(string facilityId, CancellationToken cancellationToken = default) 
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.SupplementalQueries = null;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }
    public async Task DeleteInitialQueriesForFacility(string facilityId, CancellationToken cancellationToken = default) 
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.InitialQueries = null;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }

    public async Task SaveInitialQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken)
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.InitialQueries = config;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }

    public async Task SaveSupplementalQueries(string facilityId, Dictionary<string, IQueryConfig> config, CancellationToken cancellationToken)
    {
        var filter = Builders<QueryPlan>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.SupplementalQueries = config;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }
}
