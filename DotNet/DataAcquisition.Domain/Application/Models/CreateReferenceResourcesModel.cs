using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace DataAcquisition.Domain.Application.Models
{
    public class CreateReferenceResourcesModel
    {
        public string FacilityId { get; set; }
        public string ResourceId { get; set; }
        public string ResourceType { get; set; }
        public string ReferenceResource { get; set; }
        public QueryPhase QueryPhase { get; set; }
    }
}
