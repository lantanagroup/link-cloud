using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static Census.Settings.CensusConstants;

namespace Census.Repositories;

public class CensusConfigMongoRepository : MongoDbRepository<CensusConfigEntity>, ICensusConfigMongoRepository 
{
    public CensusConfigMongoRepository(IOptions<MongoConnection> mongoSettings) : base(mongoSettings)
    {
    }

    public override async Task<CensusConfigEntity> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CensusConfigEntity>.Filter.Eq(x => x.FacilityID, facilityId);
        var config = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return config;
    }

    public override CensusConfigEntity Get(string facilityId)
    {
        var filter = Builders<CensusConfigEntity>.Filter.Eq(x => x.FacilityID, facilityId);
        var config = _collection.Find(filter).FirstOrDefault();
        return config;
    }

    public async Task<List<CensusConfigEntity>> GetAllFacilities(CancellationToken cancellationToken = default)
    {
        try
        {
            var facilities = await _collection.Find(new BsonDocument()).ToListAsync();

            return facilities;
        } catch
        {
            throw;
        }
        
    }

    public override CensusConfigEntity Update(CensusConfigEntity entity)
    {
        entity.ModifyDate = DateTime.UtcNow;
        var filter = Builders<CensusConfigEntity>.Filter.Eq(x => x.FacilityID, entity.FacilityID);
        var result = _collection.ReplaceOne(filter, entity);
        return entity;
    }

    public override async Task<CensusConfigEntity> UpdateAsync(CensusConfigEntity entity, CancellationToken cancellationToken = default)
    {
        entity.ModifyDate = DateTime.UtcNow;
        var filter = Builders<CensusConfigEntity>.Filter.Eq(x => x.FacilityID, entity.FacilityID);
        var result = await _collection.ReplaceOneAsync(filter, entity);
        return entity;
    }

    public override void Delete(string facilityId)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var filter = Builders<CensusConfigEntity>.Filter.Eq(x => x.FacilityID, facilityId);
        _collection.DeleteOne(filter);
    }

    public override async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        CheckIfNullOrString(facilityId, nameof(facilityId));
        var filter = Builders<CensusConfigEntity>.Filter.Eq(x => x.FacilityID, facilityId);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    private bool CheckIfNullOrString(object value, string varName)
    {
        if (value == null) throw new ArgumentNullException(varName);
        if (value.GetType() != typeof(string)) throw new ArgumentException($"{varName} is required to be a string!");
        return true;
    }

    public async Task<bool> HealthCheck()
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(CensusLoggingIds.HealthCheck, "Census Service - Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }

        return true;
    }
}
