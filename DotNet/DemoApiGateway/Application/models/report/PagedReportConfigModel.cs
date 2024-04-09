namespace LantanaGroup.Link.DemoApiGateway.Application.models.tenant
{
    public class PagedReportConfigModel
    {
        //generate a paged list of reports
        public List<ReportConfigModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedReportConfigModel() 
        { 
            Records = new List<ReportConfigModel>();
            Metadata = new PaginationMetadata();        
        }

        public PagedReportConfigModel(List<ReportConfigModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
