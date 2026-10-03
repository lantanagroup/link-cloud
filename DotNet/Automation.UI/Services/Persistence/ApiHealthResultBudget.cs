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
        result.ResponseSnippet = Bound(result.ResponseSnippet);
    }

    /// <summary>
    /// Returns a result whose diagnostic bodies fit the persist budget.
    /// The caller's instance is left unchanged so a live event can still carry it.
    /// </summary>
    public static ApiTestRunResult CopyWithinBudget(ApiTestRunResult result)
    {
        var copy = new ApiTestRunResult
        {
            Id = result.Id,
            RunId = result.RunId,
            EndpointKey = result.EndpointKey,
            ServiceName = result.ServiceName,
            EndpointName = result.EndpointName,
            Passed = result.Passed,
            Skipped = result.Skipped,
            SkipReason = result.SkipReason,
            ActualStatusCode = result.ActualStatusCode,
            ExpectedStatusCode = result.ExpectedStatusCode,
            ErrorMessage = result.ErrorMessage,
            ResponseSnippet = result.ResponseSnippet,
            ExecutedAt = result.ExecutedAt,
            DurationMs = result.DurationMs,
            Commit = result.Commit,
            Build = result.Build,
            Version = result.Version,
            ProductVersion = result.ProductVersion,
            RequestUrl = result.RequestUrl,
            RequestMethod = result.RequestMethod,
            RequestBody = result.RequestBody,
            TraceId = result.TraceId,
            ResponseBody = result.ResponseBody
        };
        Fit(copy);
        return copy;
    }

    internal static string? Bound(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (SnapshotPartitioner.EscapedContentBytes(value) <= MaxBodyBytes)
            return value;

        var suffixBytes = SnapshotPartitioner.EscapedContentBytes(Suffix);
        var keptBytes = 0;
        // The retained text is at most the escaped-byte budget, so a multi-megabyte
        // body must not allocate a builder the size of the input.
        var builder = new StringBuilder(Math.Min(value.Length, MaxBodyBytes + Suffix.Length));
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
