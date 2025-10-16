using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ResourceReferenceTypeModel
{
    public string FacilityId { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? ResourceType { get; set; }
    public string? FhirQueryId { get; set; }
    public FhirQueryModel? FhirQueryRef { get; set; }

    public static ResourceReferenceTypeModel FromDomain(ResourceReferenceType resourceReferenceType)
    {
        return new ResourceReferenceTypeModel
        {
            FacilityId = resourceReferenceType.FacilityId,
            QueryPhase = resourceReferenceType.QueryPhase,
            ResourceType = resourceReferenceType.ResourceType,
            FhirQueryId = resourceReferenceType.FhirQueryId,
        };
    }

    public static ResourceReferenceType ToDomain(ResourceReferenceTypeModel model)
    {
        return new ResourceReferenceType
        {
            FacilityId = model.FacilityId,
            QueryPhase = model.QueryPhase,
            ResourceType = model.ResourceType,
            FhirQueryId = model.FhirQueryId,
        };
    }
}
