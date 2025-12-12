using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities.Enums;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Report.Application.Models
{
    public class PatientSubmissionEntryUpdateModel
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public required string Id { get; set; }
        public MeasureReport? MeasureReport { get; set; }
        public string? PayloadUri { get; set; }
        public PatientSubmissionStatus Status { get; set; } = PatientSubmissionStatus.PendingEvaluation;
        public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;
    }
}
