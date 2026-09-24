using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// The synthetic FHIR MeasureReport shape for the patient report download action. JsonPropertyName
// pins the wire casing so it matches FHIR's own field names regardless of serializer defaults.
public sealed record MeasureReportResource
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; init; } = "MeasureReport";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "complete";

    [JsonPropertyName("type")]
    public string Type { get; init; } = "individual";

    [JsonPropertyName("measure")]
    public required string Measure { get; init; }

    [JsonPropertyName("date")]
    public required string Date { get; init; }

    [JsonPropertyName("reporter")]
    public MeasureReportReporter? Reporter { get; init; }

    [JsonPropertyName("period")]
    public MeasureReportPeriod? Period { get; init; }

    [JsonPropertyName("subject")]
    public required MeasureReportReference Subject { get; init; }

    [JsonPropertyName("evaluatedResource")]
    public required IReadOnlyList<MeasureReportReference> EvaluatedResource { get; init; }

    [JsonPropertyName("extension")]
    public required IReadOnlyList<MeasureReportExtension> Extension { get; init; }
}

public sealed record MeasureReportReporter
{
    [JsonPropertyName("display")]
    public required string Display { get; init; }
}

public sealed record MeasureReportPeriod
{
    [JsonPropertyName("start")]
    public required string Start { get; init; }

    [JsonPropertyName("end")]
    public required string End { get; init; }
}

public sealed record MeasureReportReference
{
    [JsonPropertyName("reference")]
    public required string Reference { get; init; }
}

public sealed record MeasureReportExtension
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("valueString")]
    public required string ValueString { get; init; }
}
