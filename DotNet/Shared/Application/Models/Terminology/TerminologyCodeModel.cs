namespace LantanaGroup.Link.Shared.Application.Models.Terminology;

/// <summary>
/// A single terminology code as returned by the code search endpoint.
/// </summary>
/// <remarks>
/// Deliberately narrower than a FHIR expansion entry: the mapping UI needs the system, the code, its
/// display and whether it is still active, and nothing else. It lives in Shared rather than in the
/// Terminology service because the LinkSdk client deserializes it and references only Shared.
/// </remarks>
public class TerminologyCodeModel
{
    /// <summary>The code system URI the code belongs to.</summary>
    public required string System { get; set; }

    /// <summary>The code value.</summary>
    public required string Code { get; set; }

    /// <summary>The human-readable display text.</summary>
    public required string Display { get; set; }

    /// <summary>The resolved status of the code.</summary>
    public CodeStatus Status { get; set; }
}
