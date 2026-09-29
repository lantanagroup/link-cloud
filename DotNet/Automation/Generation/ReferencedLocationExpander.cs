using Hl7.Fhir.Model;
using LantanaGroup.Automation.Helpers;

namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Patient/$everything does not return every Location Data Acquisition will read.
/// Location is outside the Patient compartment, and a server can still return some
/// Locations while omitting one an Encounter (or Location.partOf) references.
/// The reference query then loads that Location, normalization and org mapping
/// keep the Encounter, and ABS contains both — while the manifest never saw them.
/// </summary>
public static class ReferencedLocationExpander
{
    public const int MaxLocationReads = 100;

    public static async Task<int> AppendMissingAsync(
        IList<Bundle.EntryComponent> entries,
        Func<string, CancellationToken, Task<Location?>> readLocation,
        IAutomationOutput? output,
        CancellationToken cancellationToken,
        Uri? configuredFhirBase = null)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));
        if (readLocation == null)
            throw new ArgumentNullException(nameof(readLocation));

        // FHIR logical ids are case-sensitive. Collapsing them would skip a
        // referenced Location whose id differs only by case from one already present.
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry?.Resource is Location location && !string.IsNullOrWhiteSpace(location.Id))
                present.Add(location.Id);
        }

        var requested = new HashSet<string>(present, StringComparer.Ordinal);
        var pending = new Queue<string>();
        foreach (var entry in entries)
        {
            if (entry?.Resource == null)
                continue;
            foreach (var id in ReferencedLocationIds(entry.Resource, configuredFhirBase))
                Enqueue(requested, pending, id);
        }

        var added = 0;
        var reads = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // The check is before the next GET, so a partOf enqueued by the last
            // successful read is still pending here. Leaving it unread would publish
            // a short manifest as a green run.
            if (reads >= MaxLocationReads)
            {
                throw new InvalidOperationException(
                    $"Referenced Location expansion hit the {MaxLocationReads}-read cap with {pending.Count} reference(s) still unread. The import is stopping.");
            }

            reads++;
            var id = pending.Dequeue();
            var location = await readLocation(id, cancellationToken).ConfigureAwait(false);
            if (location == null)
            {
                output?.WriteLine(
                    $"  [imported] Location/{id} is referenced but not on the FHIR server.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(location.Id))
                location.Id = id;

            if (!present.Add(location.Id))
                continue;

            entries.Add(new Bundle.EntryComponent
            {
                Resource = location,
                Request = new Bundle.RequestComponent
                {
                    Method = Bundle.HTTPVerb.PUT,
                    Url = $"Location/{location.Id}"
                }
            });
            added++;
            output?.WriteLine(
                $"  [imported] Patient/$everything omitted Location/{location.Id}; included it for manifest prediction.");

            foreach (var referencedId in ReferencedLocationIds(location, configuredFhirBase))
                Enqueue(requested, pending, referencedId);
        }

        return added;
    }

    private static void Enqueue(HashSet<string> requested, Queue<string> pending, string id)
    {
        if (requested.Add(id))
            pending.Enqueue(id);
    }

    private static IEnumerable<string> ReferencedLocationIds(Base node, Uri? configuredFhirBase)
    {
        if (node is ResourceReference resourceReference
            && TryParseLocationId(resourceReference.Reference, configuredFhirBase, out var id))
        {
            // Query-plan simulation matches the literal prefix Location/{id}.
            // A same-base absolute reference has to be rewritten to that form or
            // the Location this method reads never enters the manifest.
            var relative = $"Location/{id}";
            if (!string.Equals(resourceReference.Reference, relative, StringComparison.Ordinal))
                resourceReference.Reference = relative;

            yield return id;
        }

        // EnumerateElements does not yield nested ResourceReference nodes, so a
        // Location reference on Encounter.location is invisible to it.
#pragma warning disable CS0618
        foreach (var child in node.Children())
#pragma warning restore CS0618
        {
            foreach (var nested in ReferencedLocationIds(child, configuredFhirBase))
                yield return nested;
        }
    }

    /// <summary>
    /// A relative reference must be <c>Location/{id}</c> or
    /// <c>Location/{id}/_history/{version}</c>. An absolute or protocol-relative
    /// reference must sit directly under <paramref name="configuredFhirBase"/>.
    /// The returned id is the logical id, without the history segment.
    /// </summary>
    internal static bool TryParseLocationId(string? reference, Uri? configuredFhirBase, out string locationId)
    {
        locationId = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        if (!TryGetLocationRelativePath(reference, configuredFhirBase, out var relativePath))
            return false;

        var parts = relativePath.Split('/');
        if (parts.Length == 2)
        {
            if (!string.Equals(parts[0], "Location", StringComparison.Ordinal) || !IsLogicalId(parts[1]))
                return false;

            locationId = parts[1];
            return true;
        }

        if (parts.Length == 4
            && string.Equals(parts[0], "Location", StringComparison.Ordinal)
            && string.Equals(parts[2], "_history", StringComparison.Ordinal)
            && IsLogicalId(parts[1])
            && IsLogicalId(parts[3]))
        {
            locationId = parts[1];
            return true;
        }

        return false;
    }

    /// <summary>
    /// FHIR id: 1-64 ASCII letters, digits, '-' or '.'. '.' and '..' match that
    /// character set but are path segments, so they are not requested.
    /// </summary>
    internal static bool IsLogicalId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || id is "." or "..")
            return false;

        foreach (var c in id)
        {
            var allowed = (c >= 'A' && c <= 'Z')
                          || (c >= 'a' && c <= 'z')
                          || (c >= '0' && c <= '9')
                          || c == '-'
                          || c == '.';
            if (!allowed)
                return false;
        }

        return true;
    }

    private static bool TryGetLocationRelativePath(string reference, Uri? configuredFhirBase, out string relativePath)
    {
        relativePath = string.Empty;
        if (reference.Contains("://", StringComparison.Ordinal) || reference.StartsWith("//", StringComparison.Ordinal))
        {
            if (configuredFhirBase == null)
                return false;

            Uri absolute;
            if (reference.StartsWith("//", StringComparison.Ordinal))
            {
                if (!Uri.TryCreate(configuredFhirBase.Scheme + ":" + reference, UriKind.Absolute, out absolute!))
                    return false;
            }
            else if (!Uri.TryCreate(reference, UriKind.Absolute, out absolute!))
            {
                return false;
            }

            if (!SameOrigin(absolute, configuredFhirBase))
                return false;

            var basePath = configuredFhirBase.AbsolutePath.TrimEnd('/');
            var referencePath = absolute.AbsolutePath;
            var prefix = basePath + "/";
            if (!referencePath.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            relativePath = Uri.UnescapeDataString(referencePath[prefix.Length..]);
            return true;
        }

        // A path-absolute reference such as /other/Location/abc is not relative
        // to the FHIR base, so it is not read from this server.
        if (reference.StartsWith('/'))
            return false;

        relativePath = reference;
        return true;
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;
}
