using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Models.Integration.Census;

public class CensusConfigApiModel
{
    public string FacilityId { get; set; } = string.Empty;
    public string ScheduledTrigger { get; set; } = string.Empty;
    public bool? Enabled { get; set; } = true;
}

public class CensusPatientEncounterApiModel
{
    public string Id { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string? MedicalRecordNumber { get; set; }
    public DateTime AdmitDate { get; set; }
    public DateTime? DischargeDate { get; set; }
    public string? EncounterType { get; set; }
    public string? EncounterStatus { get; set; }
    public string? EncounterClass { get; set; }
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
    public List<CensusPatientVisitIdentifierApiModel> PatientVisitIdentifiers { get; set; } = [];
    public List<CensusPatientIdentifierApiModel> PatientIdentifiers { get; set; } = [];
}

public class CensusPatientVisitIdentifierApiModel
{
    public string Id { get; set; } = string.Empty;
    public string PatientEncounterId { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
}

public class CensusPatientIdentifierApiModel
{
    public string Id { get; set; } = string.Empty;
    public string PatientEncounterId { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public DateTime CreateDate { get; set; }
}

public class CensusPatientEventApiModel
{
    public string Id { get; set; } = string.Empty;
    public string FacilityId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string SourcePatientId { get; set; } = string.Empty;
    public string? SourceVisitId { get; set; }
    public string? MedicalRecordNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public JsonElement Payload { get; set; }
    public string SourceType { get; set; } = string.Empty;
}

public class CensusFhirListApiModel
{
    public string? ResourceType { get; set; }
    public string? Status { get; set; }
    public string? Mode { get; set; }
    public List<CensusFhirListExtensionApiModel>? Extension { get; set; }
    public List<CensusFhirListEntryApiModel>? Entry { get; set; }
}

public class CensusFhirListExtensionApiModel
{
    public string? Url { get; set; }
    public JsonElement Value { get; set; }
}

public class CensusFhirListEntryApiModel
{
    public CensusFhirResourceReferenceApiModel? Item { get; set; }
}

public class CensusFhirResourceReferenceApiModel
{
    public string? Reference { get; set; }
}
