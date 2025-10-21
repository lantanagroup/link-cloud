using System.Runtime.CompilerServices;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;

/// <summary>
/// Generic configuration-driven parser for simple delimited files.
/// Uses FileParsingConfiguration to determine delimiter, column mappings, and field transformations.
/// Returns parsed rows as Dictionary&lt;string, string&gt; for flexible downstream processing.
/// </summary>
public class ConfigurableDelimitedParser : IFileParser<Dictionary<string, string>>
{
    private readonly ILogger<ConfigurableDelimitedParser> _logger;

    public ConfigurableDelimitedParser(ILogger<ConfigurableDelimitedParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType, string fileExtension, FileParsingConfiguration? config)
    {
        // This parser handles any file when config specifies "Delimited" parser type with mappings
        return config?.ParserType == "Delimited" && config.ColumnMappings.Count > 0;
    }

    public async IAsyncEnumerable<Dictionary<string, string>> ParseAsync(
        Stream fileStream,
        FileParsingConfiguration? config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);

        var delimiter = config.Delimiter ?? ",";
        using var reader = new StreamReader(fileStream);
        var lineNumber = 0;
        var isFirstLine = true;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Skip header if configured
            if (isFirstLine)
            {
                isFirstLine = false;
                if (config.HasHeaderRow) continue;
            }

            var record = ParseLine(line, lineNumber, delimiter, config);
            if (record != null)
                yield return record;
        }

        _logger.LogDebug("Parsed {LineCount} lines from delimited file", lineNumber);
    }

    private Dictionary<string, string>? ParseLine(
        string line,
        int lineNumber,
        string delimiter,
        FileParsingConfiguration config)
    {
        var fields = line.Split(delimiter);
        var result = new Dictionary<string, string>();

        foreach (var mapping in config.ColumnMappings)
        {
            var fieldName = mapping.Key;
            var columnIndex = mapping.Value;

            if (columnIndex < 0 || columnIndex >= fields.Length)
            {
                _logger.LogWarning(
                    "Line {LineNumber}: Column index {Index} for field '{Field}' is out of range (max: {Max})",
                    lineNumber, columnIndex, fieldName, fields.Length - 1);
                continue;
            }

            var value = fields[columnIndex]?.Trim() ?? string.Empty;

            // Apply suffix stripping if configured
            if (!string.IsNullOrEmpty(config.IdSuffixToStrip) && value.EndsWith(config.IdSuffixToStrip))
            {
                value = value[..^config.IdSuffixToStrip.Length];
            }

            result[fieldName] = value;
        }

        if (result.Count == 0)
        {
            _logger.LogWarning("Line {LineNumber}: No fields mapped, skipping", lineNumber);
            return null;
        }

        return result;
    }
}
