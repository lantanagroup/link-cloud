#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using IndexAttribute = Microsoft.EntityFrameworkCore.IndexAttribute;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("EncounterLocation")]
[Index("EncounterMappingId", Name = "IX_EncounterLocation_EncounterMappingId")]
[Index("OrganizationLocationMappingId", Name = "IX_EncounterLocation_OrganizationLocationMappingId")]
public partial class EncounterLocation
{
    [Key]
    public int EncounterLocationId { get; set; }

    public int EncounterMappingId { get; set; }

    public int OrganizationLocationMappingId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreateDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ModifiedDate { get; set; }

    [ForeignKey("EncounterMappingId")]
    [InverseProperty("EncounterLocations")]
    public virtual EncounterMapping EncounterMapping { get; set; }

    [ForeignKey("OrganizationLocationMappingId")]
    [InverseProperty("EncounterLocations")]
    public virtual OrganizationLocationMapping OrganizationLocationMapping { get; set; }
}
