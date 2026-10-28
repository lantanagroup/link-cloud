using System.Globalization;
using System.Runtime.CompilerServices;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;

/// <summary>
/// Parser for Cerner pipe-delimited census files (.dat).
/// Handles SftpAcquisitionType.Census with SftpAcquisitionSubType.CernerCCLExtract using hardcoded column positions.
/// Format: person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt
/// </summary>
public class CernerCclExtractParser(ILogger<CernerCclExtractParser> logger) : IFileParser<CernerEncounters>
{
    // Column indices for pipe-delimited format:
    // person_id|encntr_id|facility|unit|room|bed|fin|mrn|pat_nam|enc_status|enc_type|admit_dt|disch_dt
    private const int PersonIdIndex = 0;
    private const int EncntrIdIndex = 1;
    // Indices 2-5 (facility, unit, room, bed) parsed but not included in CernerEncounters model
    private const int FinIndex = 6;
    private const int MrnIndex = 7;
    // Index 8 (pat_nam) is not included in CernerEncounters model; only the connection-test preview reads it
    private const int PatNamIndex = 8;
    private const int EncStatusIndex = 9;
    private const int EncTypeIndex = 10;
    private const int AdmitDtIndex = 11;
    // Index 12 (disch_dt) parsed but not included in CernerEncounters model
    private const int MinColumnCount = 12;  // At least through admit_dt

    public bool CanParse(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType, string fileExtension, FileParsingConfiguration? config)
    {
        // This parser handles Census acquisition type with CernerCCLExtract subtype and .dat files
        return acquisitionType == SftpAcquisitionType.Census
               && subType == SftpAcquisitionSubType.CernerCCLExtract
               && fileExtension.Equals(".dat", StringComparison.OrdinalIgnoreCase);
    }

    public async IAsyncEnumerable<CernerEncounters> ParseAsync(
        Stream fileStream,
        FileParsingConfiguration? config,  // Ignored - uses hardcoded Cerner format
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var (fields, lineNumber) in ReadRowsAsync(fileStream, cancellationToken))
        {
            var encounter = ParseRow(fields, lineNumber);
            if (encounter != null)
            {
                yield return encounter;
            }
        }
    }

    /// <summary>
    /// Reads the patients in a Cerner census extract for an SFTP connection-test preview.
    /// Extract rows are per encounter, so a patient with several encounters is returned once,
    /// from the first row it appears on. Rows that <see cref="ParseAsync"/> would skip are skipped here too,
    /// so the preview shows what acquisition would actually read.
    /// </summary>
    /// <param name="fileStream">The extract file contents.</param>
    /// <param name="config">Ignored - uses hardcoded Cerner format.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async IAsyncEnumerable<SftpTestFilePatientModel> Preview(
        Stream fileStream,
        FileParsingConfiguration? config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var seenPatientIds = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var (fields, lineNumber) in ReadRowsAsync(fileStream, cancellationToken))
        {
            if (!TryGetIds(fields, lineNumber, out var patientId, out _))
            {
                continue;
            }

            if (!seenPatientIds.Add(patientId))
            {
                continue;
            }

            yield return new SftpTestFilePatientModel
            {
                PatientId = patientId,
                PatientName = GetField(fields, PatNamIndex) ?? string.Empty,
                AdmissionDate = ParseCernerDate(GetField(fields, AdmitDtIndex))
            };
        }
    }

    /// <summary>
    /// Yields the fields of each data row, skipping blank lines, the header row and rows with too few columns.
    /// </summary>
    private async IAsyncEnumerable<(string[] Fields, int LineNumber)> ReadRowsAsync(
        Stream fileStream,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fileStream);
        var lineNumber = 0;
        var isFirstLine = true;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Skip header row
            if (isFirstLine)
            {
                isFirstLine = false;
                if (line.StartsWith("person_id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var fields = line.Split('|');
            if (fields.Length < MinColumnCount)
            {
                logger.LogWarning("Line {LineNumber} has insufficient columns ({Count}/{Expected}), skipping",
                    lineNumber, fields.Length, MinColumnCount);
                continue;
            }

            yield return (fields, lineNumber);
        }
    }

    private bool TryGetIds(string[] fields, int lineNumber, out string patientId, out string encounterId)
    {
        patientId = CleanId(fields[PersonIdIndex]);
        encounterId = CleanId(fields[EncntrIdIndex]);

        if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(encounterId))
        {
            logger.LogWarning("Line {LineNumber} has empty PatientId or EncounterId, skipping", lineNumber);
            return false;
        }

        return true;
    }

    private CernerEncounters? ParseRow(string[] fields, int lineNumber)
    {
        if (!TryGetIds(fields, lineNumber, out var patientId, out var encounterId))
        {
            return null;
        }

        return new CernerEncounters
        {
            PatientId = patientId,
            EncounterId = encounterId,
            FinNumber = GetField(fields, FinIndex) ?? string.Empty,
            MRN = GetField(fields, MrnIndex) ?? string.Empty,
            EncounterStatus = GetField(fields, EncStatusIndex) ?? string.Empty,
            EncounterType = GetField(fields, EncTypeIndex) ?? string.Empty,
            AdmitDate = ParseCernerDate(GetField(fields, AdmitDtIndex)) ?? DateTime.MinValue
        };
    }

    /// <summary>
    /// Removes .00 suffix from Cerner IDs (e.g., "12345.00" -> "12345")
    /// </summary>
    private static string CleanId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.EndsWith(".00") ? trimmed[..^3] : trimmed;
    }

    private static string? GetField(string[] fields, int index)
        => index < fields.Length ? fields[index]?.Trim() : null;

    /// <summary>
    /// Parses Cerner date format: yyyyMMddHHmmss (e.g., "20230707130643")
    /// </summary>
    private static DateTime? ParseCernerDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTime.TryParseExact(value, "yyyyMMddHHmmss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        // Fallback to general parse
        return DateTime.TryParse(value, out var fallback) ? fallback : null;
    }
}
