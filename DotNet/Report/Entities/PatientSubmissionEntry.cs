using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities.Enums;
using LantanaGroup.Link.Shared.Domain.Attributes;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Entities;

[BsonCollection("patientSubmissionEntry")]
[BsonIgnoreExtraElements]
public class PatientSubmissionEntry
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }

    public string FacilityId { get; set; } = string.Empty;
    public string ReportScheduleId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;

    public MeasureReport? MeasureReport { get; set; }

    public string? PayloadUri { get; set; }

    public PatientSubmissionStatus Status { get; set; } = PatientSubmissionStatus.PendingEvaluation;
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;
}