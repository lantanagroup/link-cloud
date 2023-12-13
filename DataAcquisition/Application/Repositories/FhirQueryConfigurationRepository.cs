using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class FhirQueryConfigurationRepository : MongoDbRepository<FhirQueryConfiguration>, IFhirQueryConfigurationRepository
{
    public FhirQueryConfigurationRepository(IOptions<MongoConnection> mongoSettings) 
        : base(mongoSettings)
    {
    }

    public async Task<AuthenticationConfiguration> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirQueryConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        return queryResult?.Authentication;
    }

    public async Task SaveAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirQueryConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        
        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.Authentication = config;
            await _collection.ReplaceOneAsync(filter, queryResult);
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
        var filter = Builders<FhirQueryConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await(await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return queryResult;
    }

    public override async Task<FhirQueryConfiguration> UpdateAsync(FhirQueryConfiguration Entity, CancellationToken cancellationToken = default)
    {

        var existingEntity = await GetAsync(Entity.FacilityId, cancellationToken);

        if(existingEntity != null && existingEntity.CreateDate != null) 
        {
            Entity.Id = existingEntity.Id;
            Entity.CreateDate = existingEntity.CreateDate;   
        }
        else
        {
            Entity.CreateDate = DateTime.UtcNow;
        }

        var filter = Builders<FhirQueryConfiguration>.Filter.Eq(x => x.FacilityId, Entity.FacilityId);
        var result = await _collection.ReplaceOneAsync(filter, Entity, new ReplaceOptions { IsUpsert = true });

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

        return Entity;
    }

    public override async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        await _collection.DeleteOneAsync(x => x.FacilityId == facilityId, cancellationToken);
    }
}
