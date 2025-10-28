using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Table("PatientVisitIdentifiers")]
public class PatientVisitIdentifier : BaseEntityExtended
{
    [Key]
    public string Id { get; set; }
    public string PatientEncounterId { get; set; }
    public string Identifier { get; set; }
    public string SourceType { get; set; }
    public PatientEncounter PatientEncounter { get; set; }
}
