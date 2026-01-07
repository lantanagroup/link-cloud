using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Index("DataAcquisitionLogId", Name = "IX_ReferenceResources_DataAcquisitionLogId")]
public partial class ReferenceResources
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; }

    [Required]
    public string ResourceId { get; set; }

    [Required]
    public string ResourceType { get; set; }

    [Column("ReferenceResource")]
    public string ReferenceResource { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }

    [Required]
    public QueryPhase QueryPhase { get; set; }

    public long? DataAcquisitionLogId { get; set; }

    [ForeignKey("DataAcquisitionLogId")]
    [InverseProperty("ReferenceResources")]
    public virtual DataAcquisitionLog DataAcquisitionLog { get; set; }
}