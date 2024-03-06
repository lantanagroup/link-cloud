namespace LantanaGroup.Link.DemoApiGateway.Application.models.tenant
{
    public class PagedMeasureDefinitionConfigModel
    {
        //generate a paged list of reports
        public List<MeasureDefinitionConfigModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedMeasureDefinitionConfigModel() 
        {
            Records = new List<MeasureDefinitionConfigModel>();
            Metadata = new PaginationMetadata();
        }

        public PagedMeasureDefinitionConfigModel(List<MeasureDefinitionConfigModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
