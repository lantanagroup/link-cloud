using System.Globalization;

namespace Link.UI.Services;

/// <summary>Counts in page text: "1 broker", "2 brokers".</summary>
public static class Counts
{
    public static string Of(long count, string singular, string? plural = null) =>
        count.ToString("N0", CultureInfo.InvariantCulture) + " " + Noun(count, singular, plural);

    public static string Noun(long count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";
}
