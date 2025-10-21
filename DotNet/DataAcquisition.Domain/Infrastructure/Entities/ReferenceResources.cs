using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Index("FacilityId", "ResourceType", "ResourceId", Name = "IX_ReferenceResources_Facility_Type_ResourceId", IsUnique = true)]
public partial class ReferenceResources
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string FacilityId { get; set; }

    [Required]
    [MaxLength(256)]
    public string ResourceId { get; set; }

    [Required]
    [MaxLength(128)]
    public string ResourceType { get; set; }

    [Column("ReferenceResource")]
    public string ReferenceResource { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }

    [Required]
    public QueryPhase? QueryPhase { get; set; } = 0;

    public virtual ICollection<DataAcquisitionLog> DataAcquisitionLogs { get; set; } = new List<DataAcquisitionLog>();
}