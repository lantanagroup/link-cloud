using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class ReferenceResourcesModel
{
    public Guid Id { get; set; }
    public string FacilityId { get; set; }
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string ReferenceResource { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public long? DataAcquisitionLogId { get; set; }

    public static ReferenceResourcesModel FromDomain(ReferenceResources entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        return new ReferenceResourcesModel
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            ResourceId = entity.ResourceId,
            ResourceType = entity.ResourceType,
            ReferenceResource = entity.ReferenceResource,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate,
            QueryPhase = entity.QueryPhase,
            DataAcquisitionLogId = entity.DataAcquisitionLogId
        };
    }
}