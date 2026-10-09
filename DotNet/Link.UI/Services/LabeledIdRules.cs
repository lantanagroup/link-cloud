using System.Text.RegularExpressions;

namespace Link.UI.Services;

/// <summary>
/// A detail header shows a display name once. When the name is the identifier, the id stands alone.
/// </summary>
public static class LabeledIdRules
{
    private static readonly Regex GuidValue = new(
        "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string? DisplayName(string? name, string? value)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();
        var id = value?.Trim() ?? "";
        if (id.Length > 0 && string.Equals(trimmed, id, StringComparison.OrdinalIgnoreCase))
            return null;

        return trimmed;
    }

    /// <summary>
    /// Copy buttons belong on identifiers. Dates, seeds, counts, names, and measure lists do not get one.
    /// </summary>
    public static bool AllowsCopy(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Trim().ToLowerInvariant() switch
        {
            "seed" or "date" or "time" or "count" or "name" or "measures" or "package" => false,
            _ => true
        };
    }

    /// <summary>A GUID-style id is 8-4-4-4-12 hex, with or without braces.</summary>
    public static bool IsGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}')
            trimmed = trimmed[1..^1];

        return GuidValue.IsMatch(trimmed);
    }

    /// <summary>Copy is shown only for an allowed label whose value is a GUID.</summary>
    public static bool ShowsCopy(string? label, string? value) =>
        AllowsCopy(label) && IsGuid(value);
}
