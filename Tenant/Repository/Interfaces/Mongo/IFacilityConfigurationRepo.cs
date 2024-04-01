using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Repository.Interfaces.Mongo
{
    public interface IFacilityConfigurationRepo : IPersistenceRepository<FacilityConfigModel>
    {
        public Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken);
    }
}
