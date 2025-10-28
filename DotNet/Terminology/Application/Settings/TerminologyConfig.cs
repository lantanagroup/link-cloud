using System.ComponentModel.DataAnnotations;

namespace LantanaGroup.Link.Terminology.Application.Settings;

/// <summary>
/// Represents the configuration settings for terminology processing in the application.
/// </summary>
public class TerminologyConfig
{
    /// <summary>
    /// The path where all terminology artifacts are loaded from the server's local file system.
    /// </summary>
    public required string Path { get; init; }
}