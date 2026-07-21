#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("EncounterMapping")]
[Index("FacilityId", "EncounterId", Name = "IX_EncounterMapping_FacilityId_EncounterId")]
[Index("FacilityId", "PatientId", Name = "IX_EncounterMapping_FacilityId_PatientId")]
[Index("FacilityId", "EncounterId", Name = "UQ_EncounterMapping_FacilityId_EncounterId", IsUnique = true)]
public partial class EncounterMapping
{
    [Key]
    public int EncounterMappingId { get; set; }

    [Required]
    [StringLength(255)]
    public string FacilityId { get; set; }

    [Required]
    [StringLength(500)]
    public string PatientId { get; set; }

    [Required]
    [StringLength(500)]
    public string EncounterId { get; set; }

    public bool MappedToOrg { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreateDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ModifiedDate { get; set; }

    [InverseProperty("EncounterMapping")]
    public virtual ICollection<EncounterLocation> EncounterLocations { get; set; } = new List<EncounterLocation>();
}
