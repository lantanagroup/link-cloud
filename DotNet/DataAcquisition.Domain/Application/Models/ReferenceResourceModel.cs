using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ReferenceResourceModel
{
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
            FacilityId = referenceResource.FacilityId,
            ResourceId = referenceResource.ResourceId,
            ResourceType = referenceResource.ResourceType,
            ReferenceResource = referenceResource.ReferenceResource,
            QueryPhase = referenceResource.QueryPhase,
            DataAcquisitionLogId = referenceResource.DataAcquisitionLogId
        };
    }

    public static ReferenceResources ToDomain(ReferenceResourceModel model)
    {
        return new ReferenceResources
        {
            FacilityId = model.FacilityId,
            ResourceId = model.ResourceId,
            ResourceType = model.ResourceType,
            ReferenceResource = model.ReferenceResource,
            QueryPhase = model.QueryPhase,
            DataAcquisitionLogId = model.DataAcquisitionLogId
        };
    }
}

