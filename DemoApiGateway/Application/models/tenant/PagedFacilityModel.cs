namespace LantanaGroup.Link.DemoApiGateway.Application.models.tenant
{
    public class PagedFacilityModel
    {
        //generate a paged list of facilities
        public List<FacilityConfigModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedFacilityModel() 
        { 
            Records = new List<FacilityConfigModel>();
            Metadata = new PaginationMetadata();        
        }

        public PagedFacilityModel(List<FacilityConfigModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
