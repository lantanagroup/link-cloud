namespace LantanaGroup.Link.Census.Models;

public class UpdatePatientEncounterModel
{
    public string CorrelationId { get; set; }
    public string FacilityId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    public DateTime AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    public string? EncounterType { get; set; }
    public string? EncounterStatus { get; set; }
    public string? EncounterClass { get; set; }
    public List<PatientVisitIdentifierCreateModel> PatientVisitIdentifiers { get; set; } = new List<PatientVisitIdentifierCreateModel>();
    public List<PatientIdentifierCreateModel> PatientIdentifiers { get; set; } = new List<PatientIdentifierCreateModel>();
}