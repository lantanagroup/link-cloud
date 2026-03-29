using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace DataAcquisition.Domain.Application.Models
{
    public class UpdateReferenceResourcesModel
    {
        public Guid Id { get; set; }
        public QueryPhase QueryPhase { get; set; }
        public string ResourceType { get; set; }
        public string ReferenceResource { get; set; }
    }
}
