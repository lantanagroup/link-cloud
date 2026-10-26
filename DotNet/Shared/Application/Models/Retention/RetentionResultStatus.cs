using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// A participant's outcome for one report. There are exactly two values; a report that fails partway is
/// <see cref="Failed"/>, with the count deleted before the failure.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetentionResultStatus
{
    /// <summary>
    /// The participant finished the report, whether or not anything was left to delete.
    /// </summary>
    Completed,

    /// <summary>
    /// The participant could not finish the report.
    /// </summary>
    Failed
}
