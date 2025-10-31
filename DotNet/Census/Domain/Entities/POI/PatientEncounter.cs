using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Census.Domain.Entities.POI;

[Index("AdmitDate", Name = "IX_PatientEncounters_AdmitDate")]
[Index("CorrelationId", Name = "IX_PatientEncounters_CorrelationId")]
[Index("DischargeDate", Name = "IX_PatientEncounters_DischargeDate")]
[Index("FacilityId", Name = "IX_PatientEncounters_FacilityId")]
[Index("FacilityId", "AdmitDate", Name = "IX_PatientEncounters_FacilityId_AdmitDate")]
[Index("FacilityId", "DischargeDate", Name = "IX_PatientEncounters_FacilityId_DischargeDate")]
[Index("Id", Name = "IX_PatientEncounters_Id")]
public partial class PatientEncounter
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string CorrelationId { get; set; }

    [Required]
    public string FacilityId { get; set; }

    public string? MedicalRecordNumber { get; set; }

    [Required]
    public DateTime AdmitDate { get; set; }

    public DateTime? DischargeDate { get; set; }

    public string? EncounterType { get; set; }

    public string? EncounterStatus { get; set; }

    public string? EncounterClass { get; set; }

    public DateTime CreateDate { get; set; } = DateTime.UtcNow;

    public DateTime? ModifyDate { get; set; }

    [InverseProperty("PatientEncounter")]
    public virtual ICollection<PatientIdentifier> PatientIdentifiers { get; set; } = new List<PatientIdentifier>();

    [InverseProperty("PatientEncounter")]
    public virtual ICollection<PatientVisitIdentifier> PatientVisitIdentifiers { get; set; } = new List<PatientVisitIdentifier>();
}
