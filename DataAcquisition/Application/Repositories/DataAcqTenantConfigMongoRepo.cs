using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using static LantanaGroup.Link.DataAcquisition.Settings.DataAcquisitionConstants;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class DataAcqTenantConfigMongoRepo : MongoDbRepository<TenantDataAcquisitionConfigModel>
{
    private readonly ILogger<DataAcqTenantConfigMongoRepo> _logger;

    public DataAcqTenantConfigMongoRepo(IOptions<MongoConnection> mongoSettings, ILogger<DataAcqTenantConfigMongoRepo> logger) 
        : base(mongoSettings)
    {
        _collection = _database.GetCollection<TenantDataAcquisitionConfigModel>("DataAcquisitionTenantConfig");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void Add(TenantDataAcquisitionConfigModel entity)
    {
        try
        {
            entity.Id = Guid.NewGuid().ToString();
            _collection.InsertOne(entity);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public override async Task AddAsync(TenantDataAcquisitionConfigModel entity, CancellationToken cancellationToken = default)
    {
        try
        {
            entity.Id = Guid.NewGuid().ToString();
            await _collection.InsertOneAsync(entity);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public override void Delete(string tenantId)
    {
        _collection.DeleteOne(x => x.TenantId == tenantId);
    }

    public override async Task DeleteAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(x => x.TenantId == tenantId, cancellationToken);
    }

    public override TenantDataAcquisitionConfigModel Get(string tenantId)
    {
        var set = _collection.Find(x => x.TenantId == tenantId);
        return set.FirstOrDefault();
    }

    public override async Task<TenantDataAcquisitionConfigModel> GetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var set = await _collection.FindAsync(x => x.TenantId == tenantId);
        return await set.FirstOrDefaultAsync();
    }

    public override TenantDataAcquisitionConfigModel Update(TenantDataAcquisitionConfigModel entity)
    {
        //update definitions will need to be set by implementing class
        throw new NotImplementedException();
    }

    public override async Task<TenantDataAcquisitionConfigModel> UpdateAsync(TenantDataAcquisitionConfigModel Entity, CancellationToken cancellationToken = default)
    {
        if(Entity == null) throw new ArgumentNullException(nameof(Entity));
        if (string.IsNullOrWhiteSpace(Entity.Id)) throw new ArgumentNullException(nameof(Entity.Id));

        var filter = Builders<TenantDataAcquisitionConfigModel>.Filter.Eq(x => x.Id, Entity.Id);
        await _collection.ReplaceOneAsync(filter, Entity, cancellationToken: cancellationToken);
        return Entity;
    }

    public async Task<TenantDataAcquisitionConfigModel> GetConfigByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var facilities = await _collection.FindAsync(x => x.Facilities.Any(y => y.FacilityId == facilityId));
        var facility = facilities.FirstOrDefault();
        return facility;
        //return await (await _collection.FindAsync(x => x.Facilities.Any(y => y.FacilityId == facilityId))).FirstOrDefaultAsync();
    }

    public async Task<bool> HealthCheck()
    {
        try
        {
            await _database.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
        }
        catch (Exception ex)
        {
            _logger.LogError(new EventId(LoggingIds.HealthCheck, "Data Acquisition Service - Database Health Check"), ex, "Health check failed for database connection.");
            return false;
        }

        return true;
    }
}