using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ReferenceResourceModel
{
    public string FacilityId { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ReferenceResource { get; set; } = string.Empty;
    public QueryPhaseModel QueryPhase { get; set; }
    public string? DataAcquisitionLogId { get; set; }

    public static ReferenceResourceModel FromDomain(ReferenceResources referenceResource)
    {
        return new ReferenceResourceModel
        {
            FacilityId = referenceResource.FacilityId,
            ResourceId = referenceResource.ResourceId,
            ResourceType = referenceResource.ResourceType,
            ReferenceResource = referenceResource.ReferenceResource ?? string.Empty,
            QueryPhase = QueryPhaseModelUtilities.FromDomain(referenceResource.QueryPhase),
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
            QueryPhase = QueryPhaseModelUtilities.ToDomain(model.QueryPhase),
            DataAcquisitionLogId = model.DataAcquisitionLogId
        };
    }
}

