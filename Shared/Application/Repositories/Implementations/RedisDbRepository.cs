using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Interfaces;
using LantanaGroup.Link.Shared.Domain.Entities;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Repositories.Implementations;

public partial class RedisDbRepository<T> : IRedisRepository<T> where T : BaseEntity
{
    protected readonly ConnectionMultiplexer _redisConnection;
    protected readonly IDatabase _database;
    protected readonly RedisConnection _connectionSettings;

    public RedisDbRepository(RedisConnection connectionSettings)
    {
        _connectionSettings = connectionSettings ?? throw new ArgumentNullException(nameof(connectionSettings));
        _redisConnection = ConnectionMultiplexer.Connect(new ConfigurationOptions
        {
            EndPoints = { connectionSettings.Endpoint }
        });
        _database = connectionSettings.DatabaseNumber.HasValue ? _redisConnection.GetDatabase(connectionSettings.DatabaseNumber.Value) : _redisConnection.GetDatabase();
    }

    public virtual void Add(T entity)
    {
        try
        {
            var entityJson = SerializeObjectAsJson(entity);
            _database.StringSet(entity.Id, entityJson, TimeSpan.Parse(_connectionSettings.Expiry));
        }
        catch (Exception ex)
        {
            throw;
        }

    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        try
        {
            var entityJson = SerializeObjectAsJson(entity);
            await _database.StringSetAsync(entity.Id, entityJson, TimeSpan.Parse(_connectionSettings.Expiry));
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public virtual void Delete(string id)
    {
        _database.StringGetDelete(id as string);
    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _database.StringGetDeleteAsync(id as string);
    }

    public virtual T Get(string id)
    {
        var resultJson = _database.StringGet(id as string);
        return DeserializeJson(resultJson);
    }

    public virtual async Task<T> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var resultJson = await _database.StringGetAsync(id as string);
        return DeserializeJson(resultJson);
    }

    public virtual T Update(T entity)
    {
        var entityJson = SerializeObjectAsJson(entity);
        _database.StringSetAndGet(entity.Id, entityJson, TimeSpan.Parse(_connectionSettings.Expiry));
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entityJson = SerializeObjectAsJson(entity);
        await _database.StringSetAndGetAsync(entity.Id, entityJson, TimeSpan.Parse(_connectionSettings.Expiry));
        return entity;
    }

    protected virtual string SerializeObjectAsJson(object obj)
    {
        return JsonConvert.SerializeObject(obj);
    }

    protected virtual T DeserializeJson(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }
}
