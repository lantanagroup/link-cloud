using CsvHelper.Configuration.Attributes;

namespace LantanaGroup.Link.Terminology.Application.Models;

/// <summary>
/// Represents a record of a CSV file that corresponds to a value set entry.
/// Each record contains details about a value set, including its system, code, and display name.
/// This class serves as a base class for processing CSV files of value set mappings.
/// </summary>
public class CsvValueSetRecord
{
    /// <summary>
    /// Represents the system, typically defined as a URI, associated with the code in a value set record.
    /// </summary>
    [Index(0)]
    public required string System { get; set; }

    /// <summary>
    /// Represents the code associated with a value set record.
    /// </summary>
    [Index(1)]
    public required string Code { get; set; }

    /// <summary>
    /// Represents the human-readable display text associated with a specific code in a value set record.
    /// </summary>
    [Index(2)]
    public required string Display { get; set; }
}