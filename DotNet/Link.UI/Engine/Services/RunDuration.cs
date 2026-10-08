using System.Globalization;
using System.Text.RegularExpressions;

namespace Link.UI.Services;

/// <summary>
/// Duration text shared by the run engine and the Automation read model.
/// The engine project compiles this file on its own, so the methods stay here.
/// </summary>
public static class RunDuration
{
    public static string? Resolve(
        string? stored,
        string? status,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        DateTimeOffset createdAt)
    {
        if (TryParseDuration(stored, out var seconds))
            return seconds > 0 && seconds < 1 ? "< 1s" : FormatDuration(seconds);

        if (!string.IsNullOrWhiteSpace(stored))
            return stored.Trim();

        if (!IsTerminal(status) || finishedAt is null)
            return null;

        return FormatWallClock(finishedAt.Value - (startedAt ?? createdAt));
    }

    public static bool TryParseDuration(string? stored, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(stored))
            return false;

        var text = stored.Trim();
        if (text.Equals("< 1s", StringComparison.OrdinalIgnoreCase))
        {
            seconds = 0.5;
            return true;
        }

        var clock = Regex.Match(text, @"^(?:(\d+):)?(\d+):(\d{2})$");
        if (clock.Success)
        {
            var hours = clock.Groups[1].Success ? int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var minutes = int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
            var secs = int.Parse(clock.Groups[3].Value, CultureInfo.InvariantCulture);
            seconds = hours * 3600 + minutes * 60 + secs;
            return true;
        }

        var words = Regex.Match(
            text,
            @"^(?:(\d+)\s*h)?\s*(?:(\d+)\s*m)?\s*(?:(\d+(?:\.\d+)?)\s*s)?$",
            RegexOptions.IgnoreCase);
        if (words.Success && words.Value.Length == text.Length && words.Groups.Cast<Group>().Skip(1).Any(group => group.Success))
        {
            var hours = words.Groups[1].Success ? double.Parse(words.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var minutes = words.Groups[2].Success ? double.Parse(words.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            var secs = words.Groups[3].Success ? double.Parse(words.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
            seconds = hours * 3600 + minutes * 60 + secs;
            return seconds >= 0 && (hours > 0 || minutes > 0 || secs > 0 || text.Contains('0'));
        }

        return false;
    }

    public static string FormatWallClock(TimeSpan span)
    {
        if (span.TotalSeconds < 1)
            return span.TotalSeconds > 0 ? "< 1s" : "0:00";

        return FormatDuration(span.TotalSeconds);
    }

    public static string FormatDuration(double seconds)
    {
        if (seconds <= 0)
            return "—";

        var span = TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1
            ? span.ToString(@"h\:mm\:ss")
            : span.ToString(@"m\:ss");
    }

    private static bool IsTerminal(string? status) =>
        status is "Succeeded" or "Failed" or "Cancelled";
}
