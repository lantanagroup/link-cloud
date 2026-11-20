using System.Text.Json;
using LantanaGroup.Link.Sdk.ApiClient;
using Link.UI.Models;

namespace Link.UI.Services;

/// <summary>
/// A refused vendor or vendor-version delete. The tenant service answers 409 with Problem Details
/// whose <c>detail</c> names the facilities still holding the row. That text is what an administrator
/// has to act on, so it is shown in a dialog rather than as the raw status and body.
/// </summary>
public static class VendorDeleteNotice
{
    public const string InUseTitle = "Vendor In Use";
    public const string FailedTitle = "Delete Failed";
    public const string VendorInUseFallback = "This vendor is in use by a facility configuration and cannot be deleted.";
    public const string VersionInUseFallback = "This version is in use by a facility configuration and cannot be deleted.";
    public const string VendorFailedFallback = "Failed to delete the vendor. Please try again.";
    public const string VersionFailedFallback = "Failed to delete the version. Please try again.";

    public static ConfigurationAction From(LinkApiResponse response, bool version)
    {
        var detail = Detail(response.RawBody);
        var trace = First(response.TraceId, TraceId(response.RawBody));
        if (response.StatusCode == 409)
        {
            return ConfigurationAction.Dialog(
                InUseTitle,
                string.IsNullOrWhiteSpace(detail) ? (version ? VersionInUseFallback : VendorInUseFallback) : detail);
        }

        var message = string.IsNullOrWhiteSpace(detail)
            ? response.StatusCode == 0
                ? "Tenant could not be reached."
                : version ? VersionFailedFallback : VendorFailedFallback
            : detail;
        if (!string.IsNullOrWhiteSpace(trace) && message.IndexOf(trace, StringComparison.Ordinal) < 0)
            message += " Trace id: " + trace;
        return ConfigurationAction.Dialog(FailedTitle, message);
    }

    public static string? Detail(string? body)
    {
        if (!TryObject(body, out var root))
            return null;
        if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            return Clean(detail.GetString());
        return null;
    }

    public static string? TraceId(string? body)
    {
        if (!TryObject(body, out var root))
            return null;
        if (root.TryGetProperty("traceId", out var trace) && trace.ValueKind == JsonValueKind.String)
            return Clean(trace.GetString());
        return null;
    }

    private static string? First(string? primary, string? fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback : primary.Trim();

    private static bool TryObject(string? body, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(body))
            return false;
        var text = body.Trim();
        if (!text.StartsWith('{'))
            return false;
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            root = doc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.ReplaceLineEndings(" ").Trim();
        return text.Length == 0 ? null : text;
    }
}
