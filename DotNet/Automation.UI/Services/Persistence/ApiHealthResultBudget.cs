using System.Text;
using Automation.UI.Models.ApiHealth;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Keeps one API Health result document under the Cosmos 2 MB cap.
/// Diagnostic bodies are shortened only when they would dominate the document.
/// </summary>
internal static class ApiHealthResultBudget
{
    public const int MaxBodyBytes = 256 * 1024;
    private const string Suffix = " [truncated: exceeded document budget]";

    public static void Fit(ApiTestRunResult result)
    {
        result.ResponseBody = Bound(result.ResponseBody);
        result.RequestBody = Bound(result.RequestBody);
        result.ErrorMessage = Bound(result.ErrorMessage);
    }

    internal static string? Bound(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (SnapshotPartitioner.EscapedContentBytes(value) <= MaxBodyBytes)
            return value;

        var suffixBytes = SnapshotPartitioner.EscapedContentBytes(Suffix);
        var keptBytes = 0;
        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var runeBytes = SnapshotPartitioner.EscapedContentBytes(runeText);
            if (keptBytes + runeBytes + suffixBytes > MaxBodyBytes)
                break;

            builder.Append(runeText);
            keptBytes += runeBytes;
        }

        builder.Append(Suffix);
        return builder.ToString();
    }
}
