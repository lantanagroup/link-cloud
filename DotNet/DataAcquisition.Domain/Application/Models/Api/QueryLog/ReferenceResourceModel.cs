using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class ReferenceResourceModel
{
    public Guid? Id { get; set; }
    public string FacilityId { get; set; }
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string ReferenceResource { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public long? DataAcquisitionLogId { get; set; }

    public static ReferenceResourceModel FromDomain(ReferenceResources referenceResource)
    {
        return new ReferenceResourceModel
        {
            Id = referenceResource.Id,
            FacilityId = referenceResource.FacilityId,
            ResourceId = referenceResource.ResourceId,
            ResourceType = referenceResource.ResourceType,
            ReferenceResource = referenceResource.ReferenceResource,
            QueryPhase = referenceResource.QueryPhase,
            DataAcquisitionLogId = referenceResource.DataAcquisitionLogId
        };
    }
}

