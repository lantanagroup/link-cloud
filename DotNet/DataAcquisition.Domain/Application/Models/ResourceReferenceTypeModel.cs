using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ResourceReferenceTypeModel
{
    public string FacilityId { get; set; }
    public QueryPhaseModel QueryPhase { get; set; }
    public string? ResourceType { get; set; }
    public string? FhirQueryId { get; set; }
    public FhirQueryModel? FhirQueryRef { get; set; }

    public static ResourceReferenceTypeModel FromDomain(ResourceReferenceType resourceReferenceType)
    {
        return new ResourceReferenceTypeModel
        {
            FacilityId = resourceReferenceType.FacilityId,
            QueryPhase = QueryPhaseModelUtilities.FromDomain(resourceReferenceType.QueryPhase),
            ResourceType = resourceReferenceType.ResourceType,
            FhirQueryId = resourceReferenceType.FhirQueryId,
        };
    }

    public static ResourceReferenceType ToDomain(ResourceReferenceTypeModel model)
    {
        return new ResourceReferenceType
        {
            FacilityId = model.FacilityId,
            QueryPhase = QueryPhaseModelUtilities.ToDomain(model.QueryPhase),
            ResourceType = model.ResourceType,
            FhirQueryId = model.FhirQueryId,
        };
    }
}
