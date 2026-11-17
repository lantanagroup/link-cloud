using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Retention;

/// <summary>
/// The unit of a <see cref="RetentionWindow"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RetentionWindowUnit
{
    /// <summary>
    /// Calendar days.
    /// </summary>
    Days,

    /// <summary>
    /// Calendar months.
    /// </summary>
    Months
}
