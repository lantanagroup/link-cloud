
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Tenant.Repository.Interfaces.Mongo;

namespace LantanaGroup.Link.Tenant.Repository.Implementations.Mongo;


public class FacilityConfigurationRepo : BaseConfigurationRepo<FacilityConfigModel>, IFacilityConfigurationRepo
{
    private readonly ILogger<FacilityConfigurationRepo> _logger;


    public FacilityConfigurationRepo(IOptions<MongoConnection> mongoConnection, ILogger<FacilityConfigurationRepo> logger) : base(mongoConnection, logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken)
    {
        return await _collection.Find(t => t.FacilityId == facilityId).FirstOrDefaultAsync();

    }

}
