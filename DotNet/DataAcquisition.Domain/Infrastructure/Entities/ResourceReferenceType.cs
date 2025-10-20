using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("ResourceReferenceType")]
[Index("FhirQueryId", Name = "IX_ResourceReferenceType_FhirQueryId")]
public partial class ResourceReferenceType
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string FacilityId { get; set; }

    [Required]
    public QueryPhase QueryPhase { get; set; }

    public string ResourceType { get; set; }

    public Guid? FhirQueryId { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    [ForeignKey("FhirQueryId")]
    [InverseProperty("ResourceReferenceTypes")]
    public virtual FhirQuery FhirQuery { get; set; }
}
