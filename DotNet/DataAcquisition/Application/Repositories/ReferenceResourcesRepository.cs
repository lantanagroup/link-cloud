using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Repositories.Implementations;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LantanaGroup.Link.DataAcquisition.Application.Repositories;

public class ReferenceResourcesRepository : MongoDbRepository<ReferenceResources>, IReferenceResourcesRepository 
{
    public ReferenceResourcesRepository(IOptions<MongoConnection> mongoSettings) : base(mongoSettings)
    {
    }

    public async Task<List<ReferenceResources>> GetReferenceResourcesForListOfIds(List<string> ids, string facilityId, CancellationToken cancellationToken = default)
    {
        List<ReferenceResources> referenceResources = new List<ReferenceResources>();
        var facilityFilter = Builders<ReferenceResources>.Filter.Eq(x => x.FacilityId, facilityId);
        foreach(var id in ids)
        {
            var filter = facilityFilter & Builders<ReferenceResources>.Filter.Eq(x => x.ResourceId, id);
            var result = await (await _collection.FindAsync(filter)).FirstOrDefaultAsync(cancellationToken);
            if(result != null)
            {
                referenceResources.Add(result);
            }
        }
        return referenceResources;
    }
}
