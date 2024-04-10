using LantanaGroup.Link.Normalization.Application.Serializers;
using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static LantanaGroup.Link.Normalization.Application.Settings.NormalizationConstants;

namespace LantanaGroup.Link.Normalization.Application.Services;

public class ConfigRepository : MongoDbRepository<NormalizationConfigEntity>, IConfigRepository
{
    
    public ConfigRepository(IOptions<MongoConnection> mongoConnection) : base(mongoConnection)
    {
        //Init();
    }

    private static void Init()
    {
        BsonClassMap.RegisterClassMap<ConceptMapOperation>(cm =>
        {
            cm.AutoMap();
            cm.GetMemberMap(c => c.FhirConceptMap).SetSerializer(new ConceptMapBsonSerializer());
        });
    }

    public override NormalizationConfigEntity Get(string facilityId)
    {
        var filter = Builders<NormalizationConfigEntity>.Filter.Eq(x => x.FacilityId, facilityId);
        var result = _collection.Find<NormalizationConfigEntity>(filter).FirstOrDefault();
        return result;
    }

    public override async Task<NormalizationConfigEntity> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<NormalizationConfigEntity>.Filter.Eq(x => x.FacilityId, facilityId);
        var result = await(await _collection.FindAsync<NormalizationConfigEntity>(filter)).FirstOrDefaultAsync();
        return result;
    }

    public override void Delete(string facilityId)
    {
        var filter = Builders<NormalizationConfigEntity>.Filter.Eq(x => x.FacilityId, facilityId);
        _collection.DeleteOne(filter);
    }

    public async override Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<NormalizationConfigEntity>.Filter.Eq(x => x.FacilityId, facilityId);
        await _collection.DeleteOneAsync(filter);
    }

    public async Task<bool> HealthCheck()
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(NormalizationLoggingIds.HealthCheck, "Normalization Service - Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }

        return true;
    }
}
