using System.Globalization;
using System.Text.RegularExpressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;

/// <summary>
/// One HL7 v2 message split into segments, read with the encoding characters its own MSH segment declares.
/// Deliberately lenient and version-agnostic: facilities' ADT feeds are often not strictly conformant, and only the
/// fields Link reads need to be well formed.
/// </summary>
public sealed partial class Hl7Message
{
    private readonly List<Hl7Segment> _segments;
    private readonly char _repetition;
    private readonly char _component;
    private readonly char _subcomponent;
    private readonly char _escape;
    private readonly char _fieldSeparator;

    /// <summary>The message's 1-based position in its file, used to identify it in logs without any patient data.</summary>
    public int Position { get; }

    private Hl7Message(List<Hl7Segment> segments, char fieldSeparator, char component, char repetition, char escape, char subcomponent, int position)
    {
        _segments = segments;
        _fieldSeparator = fieldSeparator;
        _component = component;
        _repetition = repetition;
        _escape = escape;
        _subcomponent = subcomponent;
        Position = position;
    }

    /// <summary>
    /// Builds a message from its segment lines, the first of which must be the MSH segment.
    /// </summary>
    public static Hl7Message Parse(IReadOnlyList<string> lines, int position)
    {
        var msh = lines[0];
        var fieldSeparator = msh.Length > 3 ? msh[3] : '|';
        var encodingCharacters = msh.Length > 4 ? msh[4..].Split(fieldSeparator, 2)[0] : string.Empty;

        var component = encodingCharacters.Length > 0 ? encodingCharacters[0] : '^';
        var repetition = encodingCharacters.Length > 1 ? encodingCharacters[1] : '~';
        var escape = encodingCharacters.Length > 2 ? encodingCharacters[2] : '\\';
        var subcomponent = encodingCharacters.Length > 3 ? encodingCharacters[3] : '&';

        var segments = new List<Hl7Segment>(lines.Count);
        foreach (var line in lines)
        {
            var fields = line.Split(fieldSeparator);

            if (fields[0] == "MSH")
            {
                // MSH-1 is the separator itself, so fields are numbered one higher than their split position
                var shifted = new string[fields.Length + 1];
                shifted[0] = fields[0];
                shifted[1] = fieldSeparator.ToString();
                Array.Copy(fields, 1, shifted, 2, fields.Length - 1);
                fields = shifted;
            }

            segments.Add(new Hl7Segment(fields[0], fields));
        }

        return new Hl7Message(segments, fieldSeparator, component, repetition, escape, subcomponent, position);
    }

    /// <summary>
    /// Reads the value at a location. Returns null when it is absent, empty, or the HL7 explicit null (<c>""</c>).
    /// </summary>
    public string? GetValue(Hl7Path path)
    {
        var segment = SelectSegment(path);
        if (segment is null)
        {
            return null;
        }

        var field = GetRawField(segment, path.Field);
        if (field is null)
        {
            return null;
        }

        // MSH-1 and MSH-2 hold the delimiters, so they cannot be split on them
        if (segment.Name == "MSH" && path.Field <= 2)
        {
            return field;
        }

        var repetition = SelectRepetition(field, path);
        if (repetition is null)
        {
            return null;
        }

        if (path.Component is null)
        {
            return Clean(repetition);
        }

        var component = ElementAtOrNull(repetition.Split(_component), path.Component.Value);
        if (component is null)
        {
            return null;
        }

        if (path.Subcomponent is null)
        {
            return Clean(component);
        }

        return Clean(ElementAtOrNull(component.Split(_subcomponent), path.Subcomponent.Value));
    }

    /// <summary>
    /// Reads a CX-style list field into (id, assigning authority, type) tuples, one per repetition with an id.
    /// </summary>
    public IReadOnlyList<(string Id, string? AssigningAuthority, string? IdentifierType)> GetIdentifierList(string segmentName, int field)
    {
        var segment = _segments.FirstOrDefault(s => s.Name == segmentName);
        var raw = segment is null ? null : GetRawField(segment, field);
        if (raw is null)
        {
            return [];
        }

        var identifiers = new List<(string, string?, string?)>();
        foreach (var repetition in raw.Split(_repetition))
        {
            var components = repetition.Split(_component);
            var id = Clean(components[0]);
            if (id is not null)
            {
                identifiers.Add((id, Clean(ElementAtOrNull(components, 4)), Clean(ElementAtOrNull(components, 5))));
            }
        }

        return identifiers;
    }

    /// <summary>
    /// Whether the message has a segment with this name.
    /// </summary>
    public bool HasSegment(string segmentName) => _segments.Any(s => s.Name == segmentName);

    private Hl7Segment? SelectSegment(Hl7Path path)
    {
        var candidates = _segments.Where(s => s.Name == path.SegmentName);

        if (path.SegmentOccurrence is { } occurrence)
        {
            return candidates.Skip(occurrence - 1).FirstOrDefault();
        }

        if (path.SegmentFilterField is { } filterField)
        {
            return candidates.FirstOrDefault(s => SegmentMatchesFilter(s, filterField, path.SegmentFilterComponent, path.SegmentFilterValue!));
        }

        return candidates.FirstOrDefault();
    }

    private bool SegmentMatchesFilter(Hl7Segment segment, int field, int? component, string expected)
    {
        var raw = GetRawField(segment, field);
        if (raw is null)
        {
            return false;
        }

        return raw.Split(_repetition).Any(repetition =>
        {
            var value = component is null ? repetition : ElementAtOrNull(repetition.Split(_component), component.Value);
            return string.Equals(Clean(value), expected, StringComparison.OrdinalIgnoreCase);
        });
    }

    private string? SelectRepetition(string field, Hl7Path path)
    {
        var repetitions = field.Split(_repetition);

        if (path.Repetition is { } index)
        {
            return ElementAtOrNull(repetitions, index);
        }

        if (path.RepetitionFilterComponent is { } filterComponent)
        {
            return repetitions.FirstOrDefault(r => string.Equals(
                Clean(ElementAtOrNull(r.Split(_component), filterComponent)),
                path.RepetitionFilterValue,
                StringComparison.OrdinalIgnoreCase));
        }

        return repetitions[0];
    }

    private static string? GetRawField(Hl7Segment segment, int field) =>
        field < segment.Fields.Length ? segment.Fields[field] : null;

    private static string? ElementAtOrNull(string[] values, int oneBasedIndex) =>
        oneBasedIndex <= values.Length ? values[oneBasedIndex - 1] : null;

    private string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "\"\"")
        {
            return null;
        }

        return Unescape(value).Trim();
    }

    private string Unescape(string value)
    {
        if (!value.Contains(_escape))
        {
            return value;
        }

        var escape = _escape.ToString();
        return value
            .Replace($"{escape}F{escape}", _fieldSeparator.ToString())
            .Replace($"{escape}S{escape}", _component.ToString())
            .Replace($"{escape}T{escape}", _subcomponent.ToString())
            .Replace($"{escape}R{escape}", _repetition.ToString())
            .Replace($"{escape}E{escape}", escape);
    }

    [GeneratedRegex(@"^(?<year>\d{4})(?:(?<month>\d{2})(?:(?<day>\d{2})(?:(?<hour>\d{2})(?:(?<minute>\d{2})(?:(?<second>\d{2})(?:\.\d{1,4})?)?)?)?)?)?(?<offset>[+-]\d{4})?$", RegexOptions.CultureInvariant)]
    private static partial Regex TimestampRegex();

    /// <summary>
    /// Parses an HL7 v2 timestamp (<c>YYYY[MM[DD[HH[MM[SS[.S+]]]]]][+/-ZZZZ]</c>). A value with an offset is returned in
    /// UTC. One without is returned as sent with <see cref="DateTimeKind.Unspecified"/>, as HL7 then means the sender's
    /// local time. Returns null when the value is absent or not a valid timestamp.
    /// </summary>
    public static DateTime? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = TimestampRegex().Match(value.Trim());
        if (!match.Success)
        {
            return null;
        }

        static int Part(Match m, string name, int fallback) =>
            m.Groups[name].Success ? int.Parse(m.Groups[name].Value, CultureInfo.InvariantCulture) : fallback;

        try
        {
            var dateTime = new DateTime(
                Part(match, "year", 1),
                Part(match, "month", 1),
                Part(match, "day", 1),
                Part(match, "hour", 0),
                Part(match, "minute", 0),
                Part(match, "second", 0),
                DateTimeKind.Unspecified);

            if (!match.Groups["offset"].Success)
            {
                return dateTime;
            }

            var offset = match.Groups["offset"].Value;
            var hours = int.Parse(offset.AsSpan(1, 2), provider: CultureInfo.InvariantCulture);
            var minutes = int.Parse(offset.AsSpan(3, 2), provider: CultureInfo.InvariantCulture);
            var span = new TimeSpan(hours, minutes, 0);

            return new DateTimeOffset(dateTime, offset[0] == '-' ? -span : span).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

/// <summary>
/// One segment of an HL7 v2 message. <see cref="Fields"/> is indexed by HL7 field number; index 0 is the segment name.
/// </summary>
public sealed record Hl7Segment(string Name, string[] Fields);
