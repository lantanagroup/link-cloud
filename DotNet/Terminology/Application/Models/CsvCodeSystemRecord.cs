using CsvHelper.Configuration.Attributes;

namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Represents a record in a CSV code system import or export operation.
/// </summary>
/// <remarks>
/// This model maps CSV columns to terminology code system fields used by the application.
/// </remarks>
public class CsvCodeSystemRecord
{
    /// <summary>
    /// Represents a code value used in a terminology system.
    /// </summary>
    /// <remarks>
    /// This property stores the string representation of a code within a code system.
    /// It is required and acts as a key for associating additional information like display text.
    /// </remarks>
    [Index(0)]
    public required string Code { get; set; }

    /// <summary>
    /// Represents the display text associated with a code in a terminology system.
    /// </summary>
    /// <remarks>
    /// This property provides a human-readable representation of the value stored in the code property.
    /// It is typically used for display purposes in user interfaces or descriptive outputs.
    /// </remarks>
    [Index(1)]
    public required string Display { get; set; }

    /// <summary>
    /// The raw status cell, expected to read "Active" or "Inactive" in any casing. Empty in a
    /// two-column file, which has no status column at all.
    /// </summary>
    /// <remarks>
    /// Deliberately a string rather than a <see cref="CodeStatus"/>: CsvHelper's enum converter throws a
    /// <c>TypeConverterException</c> on any other value, and because the records are enumerated lazily that
    /// throw escapes the read loop and costs the entire code system, not the one bad row. Interpreting the
    /// cell is left to the loader, which defaults it and logs what it saw.
    /// </remarks>
    [Index(2)]
    public string? Status { get; set; }
}