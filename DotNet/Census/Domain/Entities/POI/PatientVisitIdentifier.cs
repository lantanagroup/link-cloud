using LantanaGroup.Link.Census.Application.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Index("PatientEncounterId", Name = "IX_PatientVisitIdentifiers_PatientEncounterId")]
public partial class PatientVisitIdentifier
{
    [Key]
    public Guid Id { get; set; }

    public Guid PatientEncounterId { get; set; }

    [Required]
    public string Identifier { get; set; }

    [Required]
    public SourceType SourceType { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    [ForeignKey("PatientEncounterId")]
    [InverseProperty("PatientVisitIdentifiers")]
    public virtual PatientEncounter PatientEncounter { get; set; }
}