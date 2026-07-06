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
    /// Indicates the status of the code item, Active or Inactive
    /// </summary>
    [Index(2)]
    [Default(CodeStatus.Active)]
    [EnumIgnoreCase]
    public CodeStatus Status { get; set; } = CodeStatus.Active;
}