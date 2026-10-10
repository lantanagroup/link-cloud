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
}
