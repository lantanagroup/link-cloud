using LantanaGroup.Link.Tenant.Models;

namespace LantanaGroup.Link.Tenant.Entities
{
    public class PagedFacilityConfigModel
    {
        public List<FacilityConfigModel> Records { get; set; } = new List<FacilityConfigModel>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedFacilityConfigModel() { }

        public PagedFacilityConfigModel(List<FacilityConfigModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
