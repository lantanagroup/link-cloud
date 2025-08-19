namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Represents a code (i.e. within a CodeSystem or ValueSet) with a value and display text.
/// </summary>
public class Code
{
    /// <summary>
    /// Gets or sets the unique code value associated with this instance.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Gets or sets the human-readable display text associated with the code.
    /// This property is required and designed to provide a descriptive or user-friendly
    /// representation of the code's meaning.
    /// </summary>
    public required string Display { get; set; }
}