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

    /// <summary>
    /// Enables the endpoints that replace a cached code group's codes from an uploaded CSV.
    /// Intended for testing only and must remain false in production.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>required</c> and deliberately unvalidated at startup: a missing key leaves
    /// this false, so the feature fails closed in any environment whose configuration store never got
    /// the row. The endpoints report themselves as not found while it is false.
    /// </remarks>
    public bool EnableCodeUploadEndpoint { get; init; }
}