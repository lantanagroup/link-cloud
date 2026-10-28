namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Logical resource id from a FHIR <c>Reference.reference</c> string.
/// Handles relative <c>Type/id</c>, absolute service-base URLs, history
/// suffixes, and contained <c>#id</c> refs. Lookup tables in prediction
/// are keyed by <c>Resource.id</c>, not the full URL.
/// </summary>
internal static class FhirReferenceId
{
    public static string FromReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return string.Empty;

        var value = reference.Trim();
        if (value.StartsWith('#'))
            return value[1..];

        var cut = value.IndexOfAny(['?', '#']);
        if (cut >= 0)
            value = value[..cut];

        const string history = "/_history/";
        var historyAt = value.IndexOf(history, StringComparison.OrdinalIgnoreCase);
        if (historyAt >= 0)
            value = value[..historyAt];

        var slash = value.LastIndexOf('/');
        return slash >= 0 ? value[(slash + 1)..] : value;
    }
}
