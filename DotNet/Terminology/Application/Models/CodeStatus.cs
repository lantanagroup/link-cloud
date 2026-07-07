using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// The status of a code in the CodeSet
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CodeStatus
{
    /// <summary>
    /// Indicates the code is Active and currently in use
    /// </summary>
    Active,
    
    /// <summary>
    /// Indicates the code is inactive and no longer used
    /// </summary>
    Inactive
}