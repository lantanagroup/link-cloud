using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Table("PatientVisitIdentifiers")]
public class PatientVisitIdentifier
{
    [Key]
    public int Id { get; set; }
    public int PatientEncounterId { get; set; }
    public string Identifier { get; set; }
    public string SourceType { get; set; }
    public PatientEncounter PatientEncounter { get; set; }
}
