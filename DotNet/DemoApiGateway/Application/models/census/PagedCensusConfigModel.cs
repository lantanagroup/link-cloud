using LantanaGroup.Link.Census.Application.Models;

namespace LantanaGroup.Link.DemoApiGateway.Application.models.census
{
    public class PagedCensusConfigModel
    {
        //create a pagination model for the census config model
        public List<CensusConfigModel> Records { get; set; }
        public PaginationMetadata Metadata { get; set; }

        public PagedCensusConfigModel()
        {
            Records = new List<CensusConfigModel>();
            Metadata = new PaginationMetadata();
        }

        public PagedCensusConfigModel(List<CensusConfigModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
