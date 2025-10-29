using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Table("PatientVisitIdentifiers")]
public class PatientVisitIdentifier
{
    [Key]
    public Guid Id { get; set; }
    public Guid PatientEncounterId { get; set; }
    public string Identifier { get; set; }
    public string SourceType { get; set; }
    public PatientEncounter PatientEncounter { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }
}
