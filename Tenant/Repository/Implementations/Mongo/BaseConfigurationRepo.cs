using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Bson;
using static LantanaGroup.Link.Tenant.Config.TenantConstants;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Mongo;

namespace LantanaGroup.Link.Tenant.Repository.Implementations.Mongo;

public class BaseConfigurationRepo<T> : IPersistenceRepository<T> where T : Entities.BaseEntity
{
    private readonly ILogger<BaseConfigurationRepo<T>> _logger;

    protected readonly IMongoCollection<T> _collection;

    protected readonly IMongoDatabase _database;

    protected readonly MongoClient _client;


    public BaseConfigurationRepo(IOptions<MongoConnection> mongoConnection, ILogger<BaseConfigurationRepo<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _client = new MongoClient(mongoConnection.Value.ConnectionString);

        _database = _client.GetDatabase(mongoConnection.Value.DatabaseName);

        _collection = _database.GetCollection<T>(GetCollectionName(typeof(T)));

    }

    public async virtual Task<List<T>> GetAsync(CancellationToken cancellationToken) => await _collection.Find(_ => true).ToListAsync();

    public async virtual Task<T> GetAsyncById(string id, CancellationToken cancellationToken) => await _collection.Find(x => x.Id.ToString() == id).FirstOrDefaultAsync();

    public async virtual Task<bool> CreateAsync(T newFacility, CancellationToken cancellationToken)
    {
        newFacility.Id = Guid.NewGuid();

        await _collection.InsertOneAsync(newFacility);

        return true;
    }

    private protected string GetCollectionName(Type documentType)
    {
        return (documentType.GetCustomAttributes(typeof(BsonCollectionAttribute), true).FirstOrDefault() as BsonCollectionAttribute)?.CollectionName;
    }



    public async virtual Task<bool> UpdateAsync(string id, T newFacility, CancellationToken cancellationToken)
    {
        await _collection.ReplaceOneAsync(x => x.Id.ToString() == id, newFacility);

        return true;
    }

    public async virtual Task<bool> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        await _collection.DeleteOneAsync(x => x.Id.ToString() == id);

        return true;
    }

    public async virtual Task<List<T>> FindAsync(FilterDefinition<T> filter, CancellationToken cancellationToken)
    {
        return await _collection.Find(filter).ToListAsync();
    }

    public virtual List<T> Get() => _collection.Find(_ => true).ToList();

    public virtual T GetById(string id)
    {
        return _collection.Find(x => x.Id.ToString() == id).FirstOrDefault();
    }

    public virtual bool Update(string id, T FacilityConfigModel)
    {
        _collection.ReplaceOne(x => x.Id.ToString() == id, FacilityConfigModel);

        return true;
    }

    public virtual bool Remove(string id)
    {
        _collection.DeleteOne(x => x.Id.ToString() == id);

        return true;
    }

    public virtual bool Create(T FacilityConfigModel)
    {
        _collection.InsertOne(FacilityConfigModel);

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
            _logger.LogError(new EventId(TenantLoggingIds.HealthCheck, "Tenant Service - Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }

        return true;
    }

}
