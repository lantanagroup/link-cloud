namespace LantanaGroup.Link.PatientFlowSimulator.Import;

/// <summary>
/// Outcome of posting one transaction bundle.
/// </summary>
public sealed record FhirImportResponse(bool Success, int StatusCode, string Body);
