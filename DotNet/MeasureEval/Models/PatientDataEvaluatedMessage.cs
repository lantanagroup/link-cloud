using Hl7.Fhir.Model;

namespace LantanaGroup.Link.MeasureEval.Models;

public class PatientDataEvaluatedMessage
{
    public string PatientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public MeasureReport? Result { get; set; }
    public string MeasureId { get; set; } = string.Empty;
}