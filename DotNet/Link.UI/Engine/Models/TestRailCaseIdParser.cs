using System.Globalization;

namespace Automation.UI.Models;

/// <summary>
/// Parses a TestRail case id. Blank is unset. "123" and "C123" both store 123.
/// Keep the accepted forms in step with wwwroot/js/testrail-case-id.js.
/// </summary>
public static class TestRailCaseIdParser
{
    public static bool TryParse(string? text, out int? caseId)
    {
        caseId = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var span = text.AsSpan().Trim();
        if (span.Length > 0 && (span[0] == 'C' || span[0] == 'c'))
            span = span[1..];

        if (span.Length == 0)
            return false;

        if (!int.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return false;

        caseId = value;
        return true;
    }
}
