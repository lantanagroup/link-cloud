using LantanaGroup.Link.Terminology.Application.Models;
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

    /// <summary>
    /// The number of codes returned by an expansion request that names no <c>count</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately not <c>required</c>, for the same reason as <see cref="EnableCodeUploadEndpoint"/>:
    /// the initialiser means a configuration store that never got the row still bounds every response
    /// rather than reverting to the unbounded expansion LEGLINK-968 exists to remove. Validated at
    /// startup against <see cref="MaxExpansionPageSize"/>.
    /// </remarks>
    public int DefaultExpansionPageSize { get; init; } = ExpansionDefaults.DefaultPageSize;

    /// <summary>
    /// The largest page any single expansion request can obtain.
    /// </summary>
    /// <remarks>
    /// A larger <c>count</c> is reduced to this rather than refused, and the reduced value is reported
    /// back in <c>ValueSet.expansion.parameter</c> so the response says what it actually contains. This
    /// is the ceiling on peak allocation for one request, which is the point of LEGLINK-968: it must
    /// not scale with the size of the code group.
    /// </remarks>
    public int MaxExpansionPageSize { get; init; } = ExpansionDefaults.MaxPageSize;
}
