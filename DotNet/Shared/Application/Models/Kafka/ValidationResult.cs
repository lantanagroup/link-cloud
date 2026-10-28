using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>Validation outcome retained independently of subsequent reporting stages.</summary>
public class ValidationResult
{
    [JsonPropertyName("passed")]
    public required bool Passed { get; set; }

    /// <summary>Total validation messages, including all categories and severities.</summary>
    [JsonPropertyName("messageCount")]
    public required long MessageCount { get; set; }
}
