using LantanaGroup.Link.Census.Domain.Entities.POI;

namespace LantanaGroup.Link.Census.Application.Models.Api;

public class PatientEncounterModel
{
    public string Id { get; set; }
    public string CorrelationId { get; set; }
    public string FacilityId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    public DateTime AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    public string? EncounterType { get; set; }
    public string? EncounterStatus { get; set; }
    public string? EncounterClass { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public List<PatientVisitIdentifierModel> PatientVisitIdentifiers { get; set; } = new List<PatientVisitIdentifierModel>();
    public List<PatientIdentifierModel> PatientIdentifiers { get; set; } = new List<PatientIdentifierModel>();

    public PatientEncounter ToDomain()
    {
        return new PatientEncounter
        {
            Id = this.Id,
            CorrelationId = this.CorrelationId,
            FacilityId = this.FacilityId,
            MedicalRecordNumber = this.MedicalRecordNumber,
            AdmitDate = this.AdmitDate,
            DischargeDate = this.DischargeDate,
            EncounterType = this.EncounterType,
            EncounterStatus = this.EncounterStatus,
            EncounterClass = this.EncounterClass,
            CreateDate = this.CreateDate ?? DateTime.UtcNow,
            ModifyDate = this.ModifyDate ?? DateTime.UtcNow,
            PatientVisitIdentifiers = this.PatientVisitIdentifiers.Select(p => p.ToDomain()).ToList(),
            PatientIdentifiers = this.PatientIdentifiers.Select(p => p.ToDomain()).ToList()
        };
    }

    public static PatientEncounterModel FromDomain(PatientEncounter patientEncounter)
    {
        return new PatientEncounterModel
        {
            Id = patientEncounter.Id,
            CorrelationId = patientEncounter.CorrelationId,
            FacilityId = patientEncounter.FacilityId,
            MedicalRecordNumber = patientEncounter.MedicalRecordNumber,
            AdmitDate = patientEncounter.AdmitDate,
            DischargeDate = patientEncounter.DischargeDate,
            EncounterType = patientEncounter.EncounterType,
            EncounterStatus = patientEncounter.EncounterStatus,
            EncounterClass = patientEncounter.EncounterClass,
            CreateDate = patientEncounter.CreateDate,
            ModifyDate = patientEncounter.ModifyDate,
            PatientVisitIdentifiers = patientEncounter.PatientVisitIdentifiers.Select(p => PatientVisitIdentifierModel.FromDomain(p)).ToList(),
            PatientIdentifiers = patientEncounter.PatientIdentifiers.Select(p => PatientIdentifierModel.FromDomain(p)).ToList()
        };
    }
}

public class PatientVisitIdentifierModel
{
    public string Id { get; set; }
    public string PatientEncounterId { get; set; }
    public string Identifier { get; set; }
    public string SourceType { get; set; }
    public DateTime CreateDate { get; set; }

    public PatientVisitIdentifier ToDomain()
    {
        return new PatientVisitIdentifier
        {
            Id = this.Id,
            PatientEncounterId = this.PatientEncounterId,
            Identifier = this.Identifier,
            SourceType = this.SourceType,
            CreateDate = this.CreateDate
        };
    }

    public static PatientVisitIdentifierModel FromDomain(PatientVisitIdentifier patientVisitIdentifier)
    {
        return new PatientVisitIdentifierModel
        {
            Id = patientVisitIdentifier.Id,
            PatientEncounterId = patientVisitIdentifier.PatientEncounterId,
            Identifier = patientVisitIdentifier.Identifier,
            SourceType = patientVisitIdentifier.SourceType,
            CreateDate = patientVisitIdentifier.CreateDate
        };
    }
}

public class PatientIdentifierModel
{
    public string Id { get; set; }
    public string PatientEncounterId { get; set; }
    public string Identifier { get; set; }
    public string SourceType { get; set; }
    public DateTime CreateDate { get; set; }

    public PatientIdentifier ToDomain()
    {
        return new PatientIdentifier
        {
            Id = this.Id,
            PatientEncounterId = this.PatientEncounterId,
            Identifier = this.Identifier,
            SourceType = this.SourceType,
            CreateDate = this.CreateDate
        };
    }

    public static PatientIdentifierModel FromDomain(PatientIdentifier patientIdentifier)
    {
        return new PatientIdentifierModel
        {
            Id = patientIdentifier.Id,
            PatientEncounterId = patientIdentifier.PatientEncounterId,
            Identifier = patientIdentifier.Identifier,
            SourceType = patientIdentifier.SourceType,
            CreateDate = patientIdentifier.CreateDate
        };
    }
}
