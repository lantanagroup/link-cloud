using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ResourceReferenceTypeModel
{
    public string? Id { get; set; } = Guid.NewGuid().ToString();    
    public string FacilityId { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? ResourceType { get; set; }
    public string? FhirQueryId { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }

    public static ResourceReferenceTypeModel FromDomain(ResourceReferenceType resourceReferenceType)
    {
        return new ResourceReferenceTypeModel
        {
            Id = resourceReferenceType.Id.ToString(),
            FacilityId = resourceReferenceType.FacilityId,
            QueryPhase = resourceReferenceType.QueryPhase,
            ResourceType = resourceReferenceType.ResourceType,
            FhirQueryId = resourceReferenceType.FhirQueryId.ToString(),
            CreateDate = resourceReferenceType.CreateDate,
            ModifyDate = resourceReferenceType.ModifyDate,
        };
    }

    public static ResourceReferenceType ToDomain(ResourceReferenceTypeModel model)
    {
        return new ResourceReferenceType
        {
            Id = new Guid(model.Id),
            FacilityId = model.FacilityId,
            QueryPhase = model.QueryPhase,
            ResourceType = model.ResourceType,
            FhirQueryId = new Guid(model.FhirQueryId),
            CreateDate = model.CreateDate,
            ModifyDate = model.ModifyDate,
        };
    }
}
