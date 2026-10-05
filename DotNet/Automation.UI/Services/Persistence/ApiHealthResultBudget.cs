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

        if (EscapedContentBytes(value) <= MaxBodyBytes)
            return value;

        var suffixBytes = EscapedContentBytes(Suffix);
        var keptBytes = 0;
        // The retained text is at most the escaped-byte budget, so a multi-megabyte
        // body must not allocate a builder the size of the input.
        var builder = new StringBuilder(Math.Min(value.Length, MaxBodyBytes + Suffix.Length));
        foreach (var rune in value.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var runeBytes = EscapedContentBytes(runeText);
            if (keptBytes + runeBytes + suffixBytes > MaxBodyBytes)
                break;

            builder.Append(runeText);
            keptBytes += runeBytes;
        }

        builder.Append(Suffix);
        return builder.ToString();
    }

    /// <summary>
    /// UTF-8 size of the value inside a JSON string written by the default
    /// serializer. Quotes and non-ASCII count as escapes.
    /// </summary>
    private static int EscapedContentBytes(string value)
    {
        var bytes = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"' or '&' or '\'' or '+' or '<' or '>' or '`':
                    bytes += 6;
                    break;
                case '\\' or '\b' or '\f' or '\n' or '\r' or '\t':
                    bytes += 2;
                    break;
                default:
                    if (c is < (char)0x20 or (char)0x7F)
                    {
                        bytes += 6;
                    }
                    else if (c < 0x80)
                    {
                        bytes += 1;
                    }
                    else if (char.IsHighSurrogate(c)
                             && i + 1 < value.Length
                             && char.IsLowSurrogate(value[i + 1]))
                    {
                        bytes += 12;
                        i++;
                    }
                    else
                    {
                        bytes += 6;
                    }

                    break;
            }
        }

        return bytes;
    }
}
