using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.Report.Repositories
{
    public class SharedResourceRepository : MongoDbRepository<SharedResourceModel>
    {
        public SharedResourceRepository(IOptions<MongoConnection> mongoSettings, ILogger<SharedResourceRepository> logger) : base(mongoSettings, logger) 
        { 
        }

        public async Task<SharedResourceModel> GetAsync(string facilityId, string resouceId, string resourceType)
        {
            var filter = Builders<SharedResourceModel>.Filter.Eq(x => x.ResourceId, resouceId)
                & Builders<SharedResourceModel>.Filter.Eq(x => x.ResourceType, resourceType)
                & Builders<SharedResourceModel>.Filter.Eq(x => x.FacilityId, facilityId);

            var entity = await _collection.Find(filter).FirstOrDefaultAsync();

            return entity;
        }
    }
}
