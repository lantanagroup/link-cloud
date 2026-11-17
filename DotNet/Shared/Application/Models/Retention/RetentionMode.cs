using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// How much of a report's data a cleanup deletes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetentionMode
{
    /// <summary>
    /// Clinical data only. What the participant normally keeps for a report stays.
    /// </summary>
    Scrub,

    /// <summary>
    /// Everything the participant holds for the report. Sent only for an administrator's permanent
    /// delete of one report.
    /// </summary>
    Purge
}
