namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;

/// <summary>
/// The parts of a configured validation category that the BFF adds to a report's counts.
/// </summary>
public class ValidationCategory
{
    /// <summary>
    /// The category's id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The category's severity: ERROR, WARNING or INFORMATION.
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// What to do about the category's findings.
    /// </summary>
    public string? Guidance { get; set; }
}
