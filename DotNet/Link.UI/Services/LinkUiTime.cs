using System.Globalization;

namespace Link.UI.Services;

/// <summary>
/// Display instants as a UTC ISO-8601 value. The browser rewrites that text into local time.
/// Unspecified values are service timestamps stored as UTC and are not shifted again.
/// A local value is converted once, to the same instant.
/// </summary>
public static class LinkUiTime
{
    public static string IsoUtc(DateTime value)
    {
        if (value == default)
            return "";

        var utc = value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Utc => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    public static string IsoUtc(DateTime? value) =>
        value is null ? "" : IsoUtc(value.Value);

    public static string IsoUtc(DateTimeOffset value) =>
        value == default
            ? ""
            : value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static string IsoUtc(DateTimeOffset? value) =>
        value is null ? "" : IsoUtc(value.Value);

    public static string Title(string? iso)
    {
        if (string.IsNullOrEmpty(iso) || iso.Length < 20 || iso[10] != 'T' || iso[^1] != 'Z')
            return iso ?? "";

        return string.Concat(iso.AsSpan(0, 10), " ", iso.AsSpan(11, 8), " UTC");
    }
}
