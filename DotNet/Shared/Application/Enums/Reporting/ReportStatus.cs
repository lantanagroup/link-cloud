using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Enums.Reporting;

/// <summary>
/// The report lifecycle derived from its patients' reporting statuses.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReportStatus
{
    Pending,
    Completed,
    CompletedNotSubmitted,
    Failure,
    Canceled
}
