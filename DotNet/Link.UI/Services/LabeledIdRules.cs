namespace Link.UI.Services;

/// <summary>
/// A detail header shows a display name once. When the name is the identifier, the id stands alone.
/// </summary>
public static class LabeledIdRules
{
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

    /// <summary>A GUID id in any form Guid.TryParse accepts (dashed, braced, or 32 hex digits).</summary>
    public static bool IsGuid(string? value)
    {
        var text = value?.Trim() ?? "";
        return text.Length > 0 && Guid.TryParse(text, out _);
    }

    /// <summary>Copy is shown only for an allowed label whose value is a GUID.</summary>
    public static bool ShowsCopy(string? label, string? value) =>
        AllowsCopy(label) && IsGuid(value);
}
