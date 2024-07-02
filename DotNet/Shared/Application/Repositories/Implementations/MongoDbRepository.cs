using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;

public class MongoDbRepository<T> : IEntityRepository<T> where T : BaseEntity
{
    private readonly ILogger<MongoDbRepository<T>> _logger;
    protected readonly IMongoCollection<T> _collection;
    protected readonly IMongoDatabase _database;
    protected readonly MongoClient _client;

    public MongoDbRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoDbRepository<T>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _client = new MongoClient(
            mongoSettings.Value.ConnectionString);

        _database = _client.GetDatabase(
            mongoSettings.Value.DatabaseName);

        _collection = _database.GetCollection<T>(GetCollectionName());

    }

    protected string GetCollectionName()
    {
        return typeof(T).GetTypeInfo().GetCustomAttribute<BsonCollectionAttribute>()?.CollectionName;
    }

    private protected string GetCollectionName(Type documentType)
    {
        return (documentType.GetCustomAttributes(typeof(BsonCollectionAttribute), true).FirstOrDefault() as BsonCollectionAttribute)?.CollectionName;
    }

    public virtual T Add(T entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Id))
            throw new EntityPotentiallyExistsException("Entity ID already has a value. This indicates that a record already exists.");

        entity.Id = Guid.NewGuid().ToString();

        try
        {
            _collection.InsertOne(entity);
        }
        catch (Exception)
        {
            throw;
        }

        return entity;  
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return null;

        if (!string.IsNullOrWhiteSpace(entity.Id))
            throw new EntityPotentiallyExistsException("Entity ID already has a value. This indicates that a record already exists.");

        entity.Id = Guid.NewGuid().ToString();

        try
        {
            await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        }
        catch (Exception)
        {
            throw;
        }

        return entity;
    }

    public virtual void Delete(string id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        _collection.DeleteOne(filter);
    }

    public virtual async System.Threading.Tasks.Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).ToListAsync(cancellationToken);
    }

    public virtual T Get(string id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        var result = _collection.Find(filter).FirstOrDefault();
        return result;
    }

    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return null;

        var result = (await _collection.FindAsync(_ => true, cancellationToken: cancellationToken)).ToList();
        return result;
    }

    public virtual T Update(T entity)
    {
        if (string.IsNullOrWhiteSpace(entity.Id))
            throw new ArgumentNullException("Entity ID");

        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        var result = _collection.ReplaceOne(filter, entity);

        if (result.MatchedCount < 0)
            return null;

        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return null;

        if (string.IsNullOrWhiteSpace(entity.Id))
            throw new ArgumentNullException("Entity ID");

        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);

        if (result.MatchedCount < 0)
            return null;

        return entity;
    }

    public async System.Threading.Tasks.Task Remove(T entity)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        await _collection.DeleteOneAsync(filter);
    }

    public async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HealthCheck(int eventId)
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(eventId, "Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }

        return true;
    }
}
