using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Table("PatientEncounters")]
public class PatientEncounter : BaseEntityExtended
{
    [Key]
    public new string Id { get; set; } = Guid.NewGuid().ToString();
    public string CorrelationId { get; set; }
    public string FacilityId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    public DateTime AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    public string? EncounterType { get; set; }
    public string? EncounterStatus { get; set; }
    public string? EncounterClass { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public ICollection<PatientVisitIdentifier> PatientVisitIdentifiers { get; set; } = new List<PatientVisitIdentifier>();
    public ICollection<PatientIdentifier> PatientIdentifiers { get; set; } = new List<PatientIdentifier>();
}
