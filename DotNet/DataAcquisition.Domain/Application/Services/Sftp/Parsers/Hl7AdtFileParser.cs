using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Parsers;

/// <summary>
/// Splits a file of HL7 v2 messages into individual messages, in file order. Handles any file extension, CR, LF or CRLF
/// segment terminators, MLLP framing characters, and batch envelopes (FHS, BHS, BTS, FTS).
/// </summary>
public partial class Hl7AdtFileParser : IFileParser<Hl7Message>
{
    private const char MllpStartBlock = '\u000B';
    private const char MllpEndBlock = '\u001C';

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentNameRegex();

    /// <inheritdoc/>
    public bool CanParse(SftpAcquisitionType acquisitionType, SftpAcquisitionSubType subType, string fileExtension, FileParsingConfiguration? config)
    {
        return acquisitionType == SftpAcquisitionType.Census && subType == SftpAcquisitionSubType.HL7ADT;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Hl7Message> ParseAsync(
        Stream fileStream,
        FileParsingConfiguration? config,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var current = new List<string>();
        var position = 0;

        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = rawLine.Trim(MllpStartBlock, MllpEndBlock, ' ', '\t');
            if (line.Length < 3 || !SegmentNameRegex().IsMatch(line[..3]) || (line.Length > 3 && char.IsLetterOrDigit(line[3])))
            {
                // Not a segment: a stray line, or a line break inside free text. Nothing Link reads lives there.
                continue;
            }

            var name = line[..3];

            if (name is "FHS" or "BHS" or "BTS" or "FTS")
            {
                continue;
            }

            if (name == "MSH")
            {
                if (current.Count > 0)
                {
                    yield return Hl7Message.Parse(current, ++position);
                }

                current = [line];
            }
            else if (current.Count > 0)
            {
                current.Add(line);
            }
        }

        if (current.Count > 0)
        {
            yield return Hl7Message.Parse(current, ++position);
        }
    }
}
