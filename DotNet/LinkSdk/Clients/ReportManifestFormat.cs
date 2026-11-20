namespace LantanaGroup.Link.Sdk.Clients;

/// <summary>
/// The shape of a manifest returned by <see cref="IReportServiceClient.GetScheduleManifestAsync"/>.
/// </summary>
public enum ReportManifestFormat
{
    /// <summary>
    /// One FHIR resource per line, as <c>application/x-ndjson</c>: the same shape as the uploaded
    /// <c>manifest.ndjson</c>.
    /// </summary>
    Ndjson,

    /// <summary>A FHIR <c>collection</c> Bundle, as <c>application/fhir+json</c>.</summary>
    Json
}
