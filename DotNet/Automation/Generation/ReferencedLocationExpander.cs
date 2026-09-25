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
        CancellationToken cancellationToken)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));
        if (readLocation == null)
            throw new ArgumentNullException(nameof(readLocation));

        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            if (entry?.Resource is Location location && !string.IsNullOrWhiteSpace(location.Id))
                present.Add(location.Id);
        }

        var requested = new HashSet<string>(present, StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>();
        foreach (var entry in entries)
        {
            if (entry?.Resource == null)
                continue;
            foreach (var id in ReferencedLocationIds(entry.Resource))
                Enqueue(requested, pending, id);
        }

        var added = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (added >= MaxLocationReads)
            {
                output?.WriteLine(
                    $"  [imported] Stopped resolving referenced Locations after {MaxLocationReads} reads.");
                break;
            }

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

            foreach (var referencedId in ReferencedLocationIds(location))
                Enqueue(requested, pending, referencedId);
        }

        return added;
    }

    private static void Enqueue(HashSet<string> requested, Queue<string> pending, string id)
    {
        if (requested.Add(id))
            pending.Enqueue(id);
    }

    private static IEnumerable<string> ReferencedLocationIds(Base node)
    {
        if (node is ResourceReference resourceReference
            && TryParseLocationId(resourceReference.Reference, out var id))
        {
            yield return id;
        }

        // EnumerateElements does not yield nested ResourceReference nodes, so a
        // Location reference on Encounter.location is invisible to it.
#pragma warning disable CS0618
        foreach (var child in node.Children())
#pragma warning restore CS0618
        {
            foreach (var nested in ReferencedLocationIds(child))
                yield return nested;
        }
    }

    internal static bool TryParseLocationId(string? reference, out string locationId)
    {
        locationId = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        var parts = reference.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        if (!string.Equals(parts[^2], "Location", StringComparison.OrdinalIgnoreCase))
            return false;

        locationId = parts[^1];
        return !string.IsNullOrWhiteSpace(locationId)
               && locationId.IndexOf('?') < 0
               && !string.Equals(locationId, "_history", StringComparison.OrdinalIgnoreCase);
    }
}
