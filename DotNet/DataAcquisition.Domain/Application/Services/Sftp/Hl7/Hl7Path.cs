using System.Text.RegularExpressions;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services.Sftp.Hl7;

/// <summary>
/// A parsed location within an HL7 v2 message: <c>SEG[selector]-Field[selector].Component.Subcomponent</c>.
/// Everything after the field is optional, numbers are 1-based, and "." is accepted in place of "-".
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><c>PID-2</c>: the whole field.</item>
/// <item><c>PID-3.1</c>: first component of the first repetition.</item>
/// <item><c>PID-3[2].1</c>: first component of the second repetition.</item>
/// <item><c>PID-3[5=MR].1</c>: first component of the repetition whose fifth component is MR.</item>
/// <item><c>OBX[2]-5</c>: OBX-5 of the second OBX segment.</item>
/// <item><c>OBX[3.1=FHIRID]-5</c>: OBX-5 of the OBX segment whose OBX-3.1 is FHIRID.</item>
/// </list>
/// </remarks>
public sealed partial record Hl7Path
{
    public required string SegmentName { get; init; }
    public int? SegmentOccurrence { get; init; }
    public int? SegmentFilterField { get; init; }
    public int? SegmentFilterComponent { get; init; }
    public string? SegmentFilterValue { get; init; }
    public required int Field { get; init; }
    public int? Repetition { get; init; }
    public int? RepetitionFilterComponent { get; init; }
    public string? RepetitionFilterValue { get; init; }
    public int? Component { get; init; }
    public int? Subcomponent { get; init; }

    [GeneratedRegex(@"^(?<segment>[A-Za-z][A-Za-z0-9]{2})(?:\[(?<segmentSelector>[^\]]+)\])?[-.](?<field>[1-9]\d{0,2})(?:\[(?<repetitionSelector>[^\]]+)\])?(?:\.(?<component>[1-9]\d{0,2})(?:\.(?<subcomponent>[1-9]\d{0,2}))?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PathRegex();

    [GeneratedRegex(@"^(?<field>[1-9]\d{0,2})(?:\.(?<component>[1-9]\d{0,2}))?=(?<value>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentFilterRegex();

    [GeneratedRegex(@"^(?<component>[1-9]\d{0,2})=(?<value>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex RepetitionFilterRegex();

    [GeneratedRegex(@"^[1-9]\d{0,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex OccurrenceRegex();

    /// <summary>
    /// Parses a location, returning false when it does not follow the syntax.
    /// </summary>
    public static bool TryParse(string? text, out Hl7Path? path)
    {
        path = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = PathRegex().Match(text.Trim());
        if (!match.Success)
        {
            return false;
        }

        int? segmentOccurrence = null, segmentFilterField = null, segmentFilterComponent = null;
        string? segmentFilterValue = null;

        if (match.Groups["segmentSelector"].Success)
        {
            var selector = match.Groups["segmentSelector"].Value;
            if (OccurrenceRegex().IsMatch(selector))
            {
                segmentOccurrence = int.Parse(selector);
            }
            else
            {
                var filter = SegmentFilterRegex().Match(selector);
                if (!filter.Success)
                {
                    return false;
                }

                segmentFilterField = int.Parse(filter.Groups["field"].Value);
                segmentFilterComponent = filter.Groups["component"].Success ? int.Parse(filter.Groups["component"].Value) : null;
                segmentFilterValue = filter.Groups["value"].Value;
            }
        }

        int? repetition = null, repetitionFilterComponent = null;
        string? repetitionFilterValue = null;

        if (match.Groups["repetitionSelector"].Success)
        {
            var selector = match.Groups["repetitionSelector"].Value;
            if (OccurrenceRegex().IsMatch(selector))
            {
                repetition = int.Parse(selector);
            }
            else
            {
                var filter = RepetitionFilterRegex().Match(selector);
                if (!filter.Success)
                {
                    return false;
                }

                repetitionFilterComponent = int.Parse(filter.Groups["component"].Value);
                repetitionFilterValue = filter.Groups["value"].Value;
            }
        }

        path = new Hl7Path
        {
            SegmentName = match.Groups["segment"].Value.ToUpperInvariant(),
            SegmentOccurrence = segmentOccurrence,
            SegmentFilterField = segmentFilterField,
            SegmentFilterComponent = segmentFilterComponent,
            SegmentFilterValue = segmentFilterValue,
            Field = int.Parse(match.Groups["field"].Value),
            Repetition = repetition,
            RepetitionFilterComponent = repetitionFilterComponent,
            RepetitionFilterValue = repetitionFilterValue,
            Component = match.Groups["component"].Success ? int.Parse(match.Groups["component"].Value) : null,
            Subcomponent = match.Groups["subcomponent"].Success ? int.Parse(match.Groups["subcomponent"].Value) : null,
        };

        return true;
    }

    /// <summary>
    /// Parses a location known to be valid, such as one that passed validation when it was saved.
    /// </summary>
    /// <exception cref="InvalidOperationException">The location does not follow the syntax.</exception>
    public static Hl7Path Parse(string text)
    {
        if (!TryParse(text, out var path))
        {
            throw new InvalidOperationException($"'{text}' is not a valid HL7 location.");
        }

        return path!;
    }
}
