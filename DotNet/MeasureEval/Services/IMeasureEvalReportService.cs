using Hl7.Fhir.Model;
using LantanaGroup.Link.MeasureEval.Models;

namespace LantanaGroup.Link.MeasureEval.Services;

public interface IMeasureEvalReportService
{
    Task<MeasureReport?> EvaluateAsync(string key, PatientDataNormalizedMessage message, string CorrelationId, CancellationToken cancellationToken = default);
    MeasureReport? Evaluate(PatientDataNormalizedMessage message);
}
