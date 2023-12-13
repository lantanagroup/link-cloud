using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class FhirQueryListConfigurationRepository : MongoDbRepository<FhirListConfiguration>, IFhirQueryListConfigurationRepository
{
    public FhirQueryListConfigurationRepository(IOptions<MongoConnection> mongoSettings) : base(mongoSettings)
    {
    }



    public async Task<AuthenticationConfiguration> GetAuthenticationConfigurationByFacilityId(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirListConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        return queryResult?.Authentication;
    }

    public async Task SaveAuthenticationConfiguration(string facilityId, AuthenticationConfiguration config, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirListConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if(queryResult != null)
        {
            queryResult.Authentication = config;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }

    public async Task DeleteAuthenticationConfiguration(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirListConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);

        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();

        if (queryResult != null)
        {
            queryResult.Authentication = null;
            await _collection.ReplaceOneAsync(filter, queryResult);
        }
    }

    public async Task<FhirListConfiguration> GetByFacilityIdAsync(string facilityId, CancellationToken cancellation = default)
    {
        var filter = Builders<FhirListConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return queryResult;
    }

    public override FhirListConfiguration Get(string facilityId)
    {
        var filter = Builders<FhirListConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = _collection.Find(filter).FirstOrDefault();
        return queryResult;
    }

    public override async Task<FhirListConfiguration> GetAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<FhirListConfiguration>.Filter.Eq(x => x.FacilityId, facilityId);
        var queryResult = await(await _collection.FindAsync(filter)).FirstOrDefaultAsync();
        return queryResult;
    }
}
