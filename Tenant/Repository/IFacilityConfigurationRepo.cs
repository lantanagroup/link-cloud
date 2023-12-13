using LantanaGroup.Link.Tenant.Entities;

namespace LantanaGroup.Link.Tenant.Repository
{
    public interface IFacilityConfigurationRepo : IPersistenceRepository<FacilityConfigModel>
    {
        public  Task<FacilityConfigModel> GetAsyncByFacilityId(string facilityId, CancellationToken cancellationToken);

        public  Task<List<FacilityConfigModel>> FindAsync(string facilityId, string id, CancellationToken cancellationToken);
    }
}
