using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class FhirQueryConfigurationRepository : BaseSqlConfigurationRepo<FhirQueryConfiguration>, IFhirQueryConfigurationRepository
{
    private readonly ILogger<FhirQueryConfigurationRepository> _logger;
    private readonly DataAcquisitionDbContext _dbContext;

    public FhirQueryConfigurationRepository(ILogger<FhirQueryConfigurationRepository> logger, DataAcquisitionDbContext dbContext) 
        : base(logger, dbContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AuthenticationConfiguration> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = _dbContext.FhirQueryConfigurations.Where(x => x.FacilityId == facilityId).FirstOrDefault();

        return queryResult?.Authentication;
    }

    public async Task SaveAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var queryResult = _dbContext.FhirQueryConfigurations.Where(x => x.FacilityId == facilityId).FirstOrDefault();

        if (queryResult != null)
        {
            queryResult.Authentication = config;
            _dbContext.FhirQueryConfigurations.Update(queryResult);
            _dbContext.SaveChanges();
        }
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirQueryConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.Authentication = null;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }

    public override async Task<FhirQueryConfiguration> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var queryResult = _dbContext.FhirQueryConfigurations.Where(x => x.FacilityId == facilityId).FirstOrDefault();
        return queryResult;
    }

    public override async Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration Entity, CancellationToken cancellationToken = default)
    {

        var existingEntity = await GetAsync(Entity.FacilityId, cancellationToken);

        if(existingEntity != null)
        {
            existingEntity.Authentication = Entity.Authentication;
            existingEntity.QueryPlanIds = Entity.QueryPlanIds;
            existingEntity.FhirServerBaseUrl = Entity.FhirServerBaseUrl;
            existingEntity.ModifyDate = DateTime.UtcNow;
        }
        else
        {
            Entity.Id = Guid.NewGuid().ToString();
            Entity.CreateDate = DateTime.UtcNow;
            Entity.ModifyDate = DateTime.UtcNow;
        }

        var filter = Builders<FhirQueryConfiguration>.Filter.Eq(x => x.FacilityId, Entity.FacilityId);
        var result = await _collection.ReplaceOneAsync(filter, existingEntity ?? Entity, new ReplaceOptions { IsUpsert = true });

        try
        {
            if(result.UpsertedId != null && string.IsNullOrWhiteSpace(Entity.Id))
            {
                Entity.Id = result.UpsertedId.AsString;
            }
        }
        catch(Exception ex)
        {
            //just returning the entity. Getting upsertedId can cause an exception.
        }

        return existingEntity ?? Entity;
    }

    public override async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(x => x.FacilityId == facilityId, cancellationToken);
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
