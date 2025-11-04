using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace DataAcquisition.Domain.Application.Models
{
    public class CreateReferenceResourcesModel
    {
        public string FacilityId { get; set; }
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public string ReferenceResource { get; set; }
        public QueryPhase QueryPhase { get; set; }
        public long? DataAcquisitionLogId { get; set; }
    }
}
