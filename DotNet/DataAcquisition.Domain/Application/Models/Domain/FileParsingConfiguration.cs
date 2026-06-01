namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;

/// <summary>
/// Configuration for parsing delimited or structured files.
/// Stored as JSON in SftpConfiguration.AcquisitionConfigurations.
/// </summary>
public class FileParsingConfiguration
{
    /// <summary>
    /// File extension this configuration applies to (e.g., ".dat", ".csv").
    /// Used to match the configuration to files during processing.
    /// </summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>
    /// Parser type: "Delimited", "FixedWidth", "Json", "Xml", or custom parser name.
    /// </summary>
    public string ParserType { get; set; } = "Delimited";

    /// <summary>
    /// Field delimiter for delimited files.
    /// </summary>
    public string Delimiter { get; set; } = "|";

    /// <summary>
    /// Whether the file has a header row to skip.
    /// </summary>
    public bool HasHeaderRow { get; set; } = true;

    /// <summary>
    /// Date format string for parsing date fields.
    /// </summary>
    public string DateFormat { get; set; } = "yyyyMMddHHmmss";

    /// <summary>
    /// Suffix to strip from ID fields (e.g., ".00" from Cerner IDs).
    /// </summary>
    public string? IdSuffixToStrip { get; set; }

    /// <summary>
    /// Maps output field names to column indices (0-based).
    /// The parser implementation determines which field names are supported.
    /// Example: { "PatientId": 0, "EncounterId": 1, "MRN": 7 }
    /// </summary>
    public Dictionary<string, int> ColumnMappings { get; set; } = new();

    /// <summary>
    /// Optional additional properties for parser-specific configuration.
    /// </summary>
    public Dictionary<string, string>? AdditionalProperties { get; set; }
}
