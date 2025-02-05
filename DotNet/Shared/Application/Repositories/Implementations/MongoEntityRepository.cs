using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;

public class MongoEntityRepository<T> : IEntityRepository<T> where T : BaseEntity
{
    private readonly ILogger<MongoEntityRepository<T>> _logger;
    protected readonly IMongoCollection<T> _collection;
    protected readonly IMongoDatabase _database;
    protected readonly MongoClient _client;

    public MongoEntityRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoEntityRepository<T>> logger)
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

    public virtual async System.Threading.Tasks.Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;
        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
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

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken) ?? (T)null;
    }

    public virtual async Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).FirstAsync(cancellationToken);
    }

    public virtual async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).SingleOrDefaultAsync(cancellationToken) ?? (T)null;
    }

    public virtual async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).SingleAsync(cancellationToken);
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

    public async System.Threading.Tasks.Task RemoveAsync(T entity)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        await _collection.DeleteOneAsync(filter);
    }

    public async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<HealthCheckResult> HealthCheck(int eventId)
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(eventId, "Database Health Check"), ex, "Health check failed for database connection.");
        }

        return HealthCheckResult.Unhealthy();
    }

    Task<(List<T>, PaginationMetadata)> IEntityRepository<T>.SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public virtual void StartTransaction()
    {
        throw new NotImplementedException();
    }

    public virtual void CommitTransaction()
    {
        throw new NotImplementedException();
    }

    public virtual void RollbackTransaction()
    {
        throw new NotImplementedException();
    }

    public virtual Task StartTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
