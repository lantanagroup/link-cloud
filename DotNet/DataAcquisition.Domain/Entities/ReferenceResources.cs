using System.ComponentModel.DataAnnotations.Schema;
using DataAcquisition.Domain.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.Shared.Domain.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("ReferenceResources")]
public class ReferenceResources : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string? ReferenceResource { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? DataAcquisitionLogId { get; set; }
    public DataAcquisitionLog? DataAcquisitionLog { get; set; }
}
