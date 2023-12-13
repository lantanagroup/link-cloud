
using LantanaGroup.Link.MeasureEval.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static LantanaGroup.Link.MeasureEval.Settings.MeasureEvalConstants;

namespace LantanaGroup.Link.MeasureEval.Repository;


public class MeasureDefinitionRepo : MongoDbRepository<MeasureDefinition>, IMeasureDefinitionRepo
{
    private readonly ILogger<MeasureDefinitionRepo> _logger;

    public MeasureDefinitionRepo(IOptions<MongoConnection> mongoSettings, ILogger<MeasureDefinitionRepo> logger)
        : base(mongoSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    //private readonly ILogger<MeasureDefinitionRepo> _logger;
    //private readonly IMongoCollection<MeasureDefinition> _collection;
    //private readonly IMongoDatabase _database;
    //private readonly MongoClient _client;

    //public MeasureDefinitionRepo(IOptions<MongoConnection> mongoConnection, ILogger<MeasureDefinitionRepo> logger) 
    //{
    //    _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    //    _client = new MongoClient(mongoConnection.Value.ConnectionString);
    //    _database = _client.GetDatabase(mongoConnection.Value.DatabaseName);
    //    _collection = _database.GetCollection<MeasureDefinition>(mongoConnection.Value.CollectionName);
    //}


    public async Task CreateAsync(MeasureDefinition entity, CancellationToken token)
    {
        entity.lastUpdated = DateTime.UtcNow;
        entity.Id = Guid.NewGuid().ToString();
        await _collection.InsertOneAsync(entity, token);
    }

    public async Task UpdateAsync(string measureDefinitionId, MeasureDefinition entity, CancellationToken cancellationToken)
    {
        entity.lastUpdated = DateTime.UtcNow;
        var filter = Builders<MeasureDefinition>.Filter.Eq(x => x.measureDefinitionId, measureDefinitionId);
        await _collection.ReplaceOneAsync(filter, entity);
    }

    public async Task<MeasureDefinition> GetAsync(string measureDefinitionId, CancellationToken cancellationToken)
    {
        var result = (await _collection.FindAsync(x => x.measureDefinitionId == measureDefinitionId)).FirstOrDefault();
        return result;
    }

    public async Task<bool> ExistsAsync(string measureDefinitionId, CancellationToken cancellationToken)
    {
        return await _collection.CountDocumentsAsync(x => x.measureDefinitionId == measureDefinitionId, cancellationToken: cancellationToken) > 0;
    }

    public async Task<bool> HealthCheck()
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(MeasureEvalLoggingIds.HealthCheck, "Measure Evaluation Service - Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }

        return true;
    }

    public async Task DeleteAsync(string measureDefinitionId, CancellationToken cancellationToken)
    {
        var filter = Builders<MeasureDefinition>.Filter.Eq(x => x.measureDefinitionId, measureDefinitionId);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

}
