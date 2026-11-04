namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;

/// <summary>
/// A report's total of validation findings in one category.
/// </summary>
public class ValidationCategoryCount
{
    /// <summary>
    /// The category's id, or "uncategorized" for findings that matched no category.
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// The category's title when the count was last written.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Whether the category's findings were acceptable when the count was last written.
    /// </summary>
    public bool Acceptable { get; set; }

    /// <summary>
    /// Whether the category's findings were submitted when the count was last written.
    /// </summary>
    public bool Submit { get; set; }

    /// <summary>
    /// The category's current severity, or null when the category no longer exists.
    /// </summary>
    public string? Severity { get; set; }

    /// <summary>
    /// The category's current guidance, or null when the category no longer exists.
    /// </summary>
    public string? Guidance { get; set; }

    /// <summary>
    /// The number of findings in the category.
    /// </summary>
    public long Issues { get; set; }
}
