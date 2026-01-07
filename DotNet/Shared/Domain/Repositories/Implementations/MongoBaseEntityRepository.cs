using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Attributes;
using LantanaGroup.Link.Shared.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using System.Reflection;

namespace LantanaGroup.Link.Shared.Domain.Repositories.Implementations;

public class MongoBaseEntityRepository<T> : IBaseEntityRepository<T> where T : BaseEntity
{
    private readonly ILogger<MongoBaseEntityRepository<T>> _logger;
    protected readonly IMongoCollection<T> _collection;
    protected readonly IMongoDatabase _database;
    protected readonly MongoClient _client;

    public MongoBaseEntityRepository(IOptions<MongoConnection> mongoSettings, ILogger<MongoBaseEntityRepository<T>> logger)
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
        entity.Id ??= Guid.NewGuid().ToString();

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

        entity.Id ??= Guid.NewGuid().ToString();

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

    public virtual void Delete(object id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        _collection.DeleteOne(filter);
    }

    public virtual async Task DeleteAsync(T? entity, CancellationToken cancellationToken = default)
    {
        if (entity is null) return;
        if (cancellationToken.IsCancellationRequested) return;
        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        await _collection.DeleteOneAsync(filter, cancellationToken);
    }

    public virtual async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
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
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).FirstOrDefaultAsync(cancellationToken) ?? null;
    }

    public virtual async Task<T> FirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).FirstAsync(cancellationToken);
    }

    public virtual async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).SingleOrDefaultAsync(cancellationToken) ?? null;
    }

    public virtual async Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await (await _collection.FindAsync(predicate, cancellationToken: cancellationToken)).SingleAsync(cancellationToken);
    }

    public virtual T Get(object id)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, id);
        var result = _collection.Find(filter).FirstOrDefault();
        return result;
    }

    public virtual async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return null;

        try
        {
            var result = (await _collection.FindAsync(_ => true, cancellationToken: cancellationToken)).ToList();
            return result;
        }
        catch (MongoCommandException ex) when (ex.Code == 26) // NamespaceNotFound
        {
            // Collection doesn't exist yet - return empty list
            _logger.LogDebug("Collection {CollectionName} does not exist yet. Returning empty list.", GetCollectionName());
            return new List<T>();
        }
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _collection.CountDocumentsAsync<T>(predicate, cancellationToken: cancellationToken) > 0;
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

    public async Task RemoveAsync(T entity)
    {
        var filter = Builders<T>.Filter.Eq(x => x.Id, entity.Id);
        await _collection.DeleteOneAsync(filter);
    }

    public async Task<T> GetAsync(object id, CancellationToken cancellationToken = default)
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

    public async Task<(List<T>, PaginationMetadata)> SearchAsync(Expression<Func<T, bool>> predicate, string? sortBy, SortOrder? sortOrder, int pageSize, int pageNumber, CancellationToken cancellationToken)
    {
        if (pageNumber < 1) throw new ArgumentException("Page number must be greater than 0.", nameof(pageNumber));
        if (pageSize < 1) throw new ArgumentException("Page size must be greater than 0.", nameof(pageSize));

        var count = await _collection.CountDocumentsAsync<T>(predicate, cancellationToken: cancellationToken);

        var skip = (pageNumber - 1) * pageSize;
        var query = _collection.Find(predicate).Skip(skip).Limit(pageSize);

        if (!string.IsNullOrEmpty(sortBy))
        {
            var sortDefinition = sortOrder == SortOrder.Descending
                ? Builders<T>.Sort.Descending(sortBy)
                : Builders<T>.Sort.Ascending(sortBy);

            query = query.Sort(sortDefinition);
        }

        var result = await query.ToListAsync(cancellationToken);

        var metadata = new PaginationMetadata(pageSize, pageNumber, count);

        return (result, metadata);
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
