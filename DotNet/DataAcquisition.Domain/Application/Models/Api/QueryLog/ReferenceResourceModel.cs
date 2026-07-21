using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

public class ReferenceResourceModel
{
    public Guid? Id { get; set; }
    public string FacilityId { get; set; }
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string ReferenceResource { get; set; }
    public QueryPhase QueryPhase { get; set; }

    public static ReferenceResourceModel FromDomain(ReferenceResources referenceResource)
    {
        return new ReferenceResourceModel
        {
            Id = referenceResource.Id,
            FacilityId = referenceResource.FacilityId,
            ResourceId = referenceResource.ResourceId,
            ResourceType = referenceResource.ResourceType,
            ReferenceResource = referenceResource.ReferenceResource,
            QueryPhase = referenceResource.QueryPhase.GetValueOrDefault()
        };
    }
}

