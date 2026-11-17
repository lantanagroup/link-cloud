using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// What started a retention cleanup. Serialized by name, as every retention enum is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetentionInitiatedBy
{
    /// <summary>
    /// The scheduled retention run.
    /// </summary>
    Scheduled,

    /// <summary>
    /// A bulk run an administrator requested.
    /// </summary>
    Manual,

    /// <summary>
    /// An administrator's scrub or permanent delete of one report.
    /// </summary>
    SingleReport
}
