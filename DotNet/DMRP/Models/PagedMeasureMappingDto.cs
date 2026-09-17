using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.DMRP.Models
{
    public class PagedMeasureMappingDto
    {
        public List<MeasureMappingModel> Records { get; set; } = new List<MeasureMappingModel>();
        public PaginationMetadata Metadata { get; set; } = null!;

        public PagedMeasureMappingDto() { }

        public PagedMeasureMappingDto(List<MeasureMappingModel> records, PaginationMetadata metadata)
        {
            Records = records;
            Metadata = metadata;
        }
    }
}
