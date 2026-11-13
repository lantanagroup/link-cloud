using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using LantanaGroup.Automation.Configuration;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;

namespace Automation.UI.Services;

/// <summary>
/// Keeps concurrent report runs on one EHR from destroying each other's work.
/// An EHR is the FHIR base URL plus the non-secret auth identity. Each run still
/// gets its own facility from <see cref="RunOrganizationIdAllocator"/>.
/// </summary>
internal static class ParallelRunSafety
{
    public const string MetricsRunBlockedMessage =
        "A Metrics run is already in progress. Metrics runs share Prometheus data, so this one cannot start until that run finishes.";

    /// <summary>
    /// Metrics runs read process-wide Prometheus series. A second one would mix
    /// those series with the first. Any other report run is allowed.
    /// </summary>
    public static bool BlocksMetricsStart(bool startingMetricsRun, bool anotherMetricsRunIsOngoing)
        => startingMetricsRun && anotherMetricsRunIsOngoing;

    /// <summary>
    /// Resource paths this run may expunge. A path another ongoing run on the same
    /// EHR still claims stays on the server.
    /// </summary>
    public static IReadOnlyList<string> PathsSafeToExpunge(
        IEnumerable<string> createdPaths,
        IEnumerable<string> heldByOtherRuns)
    {
        ArgumentNullException.ThrowIfNull(createdPaths);
        ArgumentNullException.ThrowIfNull(heldByOtherRuns);
        var held = new HashSet<string>(
            heldByOtherRuns.Where(path => !string.IsNullOrWhiteSpace(path)),
            StringComparer.Ordinal);
        return createdPaths
            .Where(path => !string.IsNullOrWhiteSpace(path) && !held.Contains(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// True when <paramref name="facilityId"/> is the facility of some other ongoing run.
    /// Mapping and service cleanup for that id would delete the other run's rows.
    /// </summary>
    public static bool FacilityIsHeldByAnotherRun(
        string? facilityId,
        IEnumerable<string> facilitiesHeldByOtherRuns)
    {
        ArgumentNullException.ThrowIfNull(facilitiesHeldByOtherRuns);
        if (string.IsNullOrWhiteSpace(facilityId))
            return false;

        return facilitiesHeldByOtherRuns.Any(id =>
            !string.IsNullOrWhiteSpace(id) && string.Equals(id, facilityId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Patient ids for the live census pool. Bundle imports have already been
    /// rewritten onto this run's tag, so the pool must use those ids. The saved
    /// scenario still carries the original id, and admitting that id would publish
    /// a patient this run did not upload.
    /// </summary>
    public static IReadOnlyList<string> PatientIdsAfterIsolate(IEnumerable<ImportedPatientInput> isolatedImports)
    {
        ArgumentNullException.ThrowIfNull(isolatedImports);
        return isolatedImports
            .Where(imported => !string.IsNullOrWhiteSpace(imported.PatientId))
            .Select(imported => imported.PatientId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

/// <summary>
/// Identity of one EHR endpoint. The client secret and password are not part of
/// the key: rotating a secret does not make the same endpoint look like a second EHR.
/// </summary>
internal static class EhrEndpointIdentity
{
    public static string From(AutomationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return From(config.FhirServerBase, config.FhirServerOAuth, config.FhirServerBasicAuth);
    }

    public static string From(string? fhirServerBase, OAuthConfig? oauth, BasicAuthConfig? basic)
    {
        var url = Normalize(fhirServerBase);
        var auth = AuthMark(oauth, basic);
        return auth.Length == 0 ? url : url + "\n" + auth;
    }

    private static string Normalize(string? fhirServerBase)
    {
        var trimmed = (fhirServerBase ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return trimmed;

        return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped)
            .TrimEnd('/');
    }

    private static string AuthMark(OAuthConfig? oauth, BasicAuthConfig? basic)
    {
        var parts = new List<string>();
        if (oauth?.ShouldAuthenticate == true)
        {
            parts.Add("oauth");
            parts.Add(oauth.TokenEndpoint?.Trim() ?? string.Empty);
            parts.Add(oauth.ClientId?.Trim() ?? string.Empty);
        }

        if (basic?.ShouldAuthenticate == true)
        {
            parts.Add("basic");
            parts.Add(basic.Username?.Trim() ?? string.Empty);
        }

        return string.Join("|", parts);
    }
}

/// <summary>
/// Gives one run its own copy of an imported bundle's logical ids. Two runs that
/// upload the same bundle to the same EHR then create different resources, and
/// one run's expunge cannot reach the other's.
/// Existing-id imports are left as they are: those resources were already on the
/// server and this run does not create them.
/// </summary>
internal static class ImportedResourceRunScope
{
    public const int MaxLogicalIdLength = 64;

    public static string RunTag(Guid runId) => runId.ToString("N")[..8];

    public static string ScopeLogicalId(string runTag, string originalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runTag);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalId);
        var prefixed = runTag + "-" + originalId;
        if (IsLegalLogicalId(prefixed))
            return prefixed;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originalId)))[..12].ToLowerInvariant();
        return runTag + "-" + hash;
    }

    /// <summary>
    /// Rewrites a bundle import onto <paramref name="runTag"/>. Returns the
    /// resource paths (<c>Type/id</c>) this run now uses.
    /// </summary>
    public static IReadOnlyList<string> Isolate(ImportedPatientInput input, string runTag)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Source != ImportedPatientSource.Bundle || string.IsNullOrWhiteSpace(input.BundleJson))
            return ResourcePaths(input);

        JsonObject root;
        try
        {
            if (JsonNode.Parse(input.BundleJson) is not JsonObject parsed)
                throw new InvalidOperationException("Imported bundle JSON must be an object.");
            root = parsed;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Imported bundle JSON could not be read.", ex);
        }

        var originals = new List<(string Type, string Id)>();
        CollectResources(root, originals);
        if (originals.Count == 0)
            return ResourcePaths(input);

        var oldPathToNewPath = BuildPathMap(runTag, originals);
        RewriteNode(root, oldPathToNewPath, ReplacementTokens(oldPathToNewPath));

        input.BundleJson = root.ToJsonString();
        input.PreLoadedEntries = null;

        var patientPath = "Patient/" + input.PatientId.Trim();
        if (oldPathToNewPath.TryGetValue(patientPath, out var newPatientPath))
        {
            input.PatientId = newPatientPath["Patient/".Length..];
        }
        else if (string.IsNullOrWhiteSpace(input.PatientId))
        {
            var firstPatient = oldPathToNewPath.FirstOrDefault(pair =>
                pair.Key.StartsWith("Patient/", StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(firstPatient.Value))
                input.PatientId = firstPatient.Value["Patient/".Length..];
        }

        return oldPathToNewPath.Values.Distinct(StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, string> ReplacementTokens(IReadOnlyDictionary<string, string> oldPathToNewPath)
    {
        var tokens = new Dictionary<string, string>(oldPathToNewPath, StringComparer.Ordinal);
        foreach (var (oldPath, newPath) in oldPathToNewPath)
        {
            var oldId = oldPath[(oldPath.IndexOf('/') + 1)..];
            var newId = newPath[(newPath.IndexOf('/') + 1)..];
            tokens.TryAdd("#" + oldId, "#" + newId);
            tokens.TryAdd("urn:uuid:" + oldId, "urn:uuid:" + newId);
        }

        return tokens;
    }

    public static IReadOnlyList<string> ResourcePaths(ImportedPatientInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.PreLoadedEntries is { Count: > 0 })
        {
            return input.PreLoadedEntries
                .Select(entry => entry?.Resource)
                .Where(resource => resource != null
                    && !string.IsNullOrWhiteSpace(resource.TypeName)
                    && !string.IsNullOrWhiteSpace(resource.Id))
                .Select(resource => resource!.TypeName + "/" + resource.Id)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        if (input.Source == ImportedPatientSource.ExistingId && !string.IsNullOrWhiteSpace(input.PatientId))
            return ["Patient/" + input.PatientId.Trim()];

        return [];
    }

    private static Dictionary<string, string> BuildPathMap(string runTag, List<(string Type, string Id)> originals)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedNewPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (type, id) in originals)
        {
            var oldPath = type + "/" + id;
            if (map.ContainsKey(oldPath))
                continue;

            var newId = ScopeLogicalId(runTag, id);
            var newPath = type + "/" + newId;
            var suffix = 2;
            while (!usedNewPaths.Add(newPath))
            {
                if (suffix > 100)
                    throw new InvalidOperationException($"Could not give {oldPath} a run-scoped id.");

                var disambiguated = newId + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!IsLegalLogicalId(disambiguated))
                    disambiguated = ScopeLogicalId(runTag, id + "-" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture));
                newPath = type + "/" + disambiguated;
                suffix++;
            }

            map[oldPath] = newPath;
        }

        return map;
    }

    private static void CollectResources(JsonNode? node, List<(string Type, string Id)> found)
    {
        switch (node)
        {
            case JsonObject obj:
                var type = StringProperty(obj, "resourceType");
                var id = StringProperty(obj, "id");
                if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(id))
                    found.Add((type, id));
                foreach (var property in obj)
                    CollectResources(property.Value, found);
                break;
            case JsonArray array:
                foreach (var item in array)
                    CollectResources(item, found);
                break;
        }
    }

    private static void RewriteNode(
        JsonNode? node,
        IReadOnlyDictionary<string, string> oldPathToNewPath,
        IReadOnlyDictionary<string, string> replacementTokens)
    {
        switch (node)
        {
            case JsonObject obj:
                var type = StringProperty(obj, "resourceType");
                var id = StringProperty(obj, "id");
                if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(id)
                    && oldPathToNewPath.TryGetValue(type + "/" + id, out var newPath))
                {
                    obj["id"] = newPath[(type.Length + 1)..];
                }

                ReplaceStringProperty(obj, "reference", replacementTokens);
                ReplaceStringProperty(obj, "fullUrl", replacementTokens);
                if (obj["request"] is JsonObject request)
                {
                    ReplaceStringProperty(request, "url", replacementTokens);
                    ReplaceStringProperty(request, "ifNoneExist", replacementTokens);
                }

                foreach (var property in obj.ToList())
                    RewriteNode(property.Value, oldPathToNewPath, replacementTokens);
                break;
            case JsonArray array:
                foreach (var item in array)
                    RewriteNode(item, oldPathToNewPath, replacementTokens);
                break;
        }
    }

    private static void ReplaceStringProperty(
        JsonObject obj,
        string name,
        IReadOnlyDictionary<string, string> replacementTokens)
    {
        var current = StringProperty(obj, name);
        if (current == null)
            return;

        var replaced = ReplacePaths(current, replacementTokens);
        if (!string.Equals(replaced, current, StringComparison.Ordinal))
            obj[name] = replaced;
    }

    internal static string ReplacePaths(string value, IReadOnlyDictionary<string, string> oldPathToNewPath)
    {
        var result = value;
        foreach (var (oldPath, newPath) in oldPathToNewPath.OrderByDescending(pair => pair.Key.Length))
            result = ReplacePathToken(result, oldPath, newPath);
        return result;
    }

    private static string ReplacePathToken(string value, string oldPath, string newPath)
    {
        var builder = new StringBuilder(value.Length);
        var index = 0;
        while (index < value.Length)
        {
            var found = value.IndexOf(oldPath, index, StringComparison.Ordinal);
            if (found < 0)
            {
                builder.Append(value, index, value.Length - index);
                break;
            }

            var beforeOk = found == 0 || !IsLogicalIdChar(value[found - 1]);
            var end = found + oldPath.Length;
            var afterOk = end >= value.Length || !IsLogicalIdChar(value[end]);
            if (beforeOk && afterOk)
            {
                builder.Append(value, index, found - index);
                builder.Append(newPath);
                index = end;
            }
            else
            {
                builder.Append(value, index, found - index + 1);
                index = found + 1;
            }
        }

        return builder.ToString();
    }

    private static string? StringProperty(JsonObject obj, string name)
    {
        if (obj[name] is JsonValue value && value.TryGetValue<string>(out var text))
            return text;
        return null;
    }

    private static bool IsLegalLogicalId(string id) =>
        id.Length is >= 1 and <= MaxLogicalIdLength && id.All(IsLogicalIdChar);

    private static bool IsLogicalIdChar(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '-' or '.';
}

/// <summary>
/// In-process record of what each ongoing run is using on an EHR. One executor
/// serves every run, so this is the shared view success cleanup consults.
/// </summary>
internal sealed class SharedEhrWorkRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Work> _runs = new();

    public void Begin(Guid runId, string ehrKey, string? facilityId)
    {
        lock (_gate)
        {
            var work = WorkFor(runId);
            work.EhrKey = ehrKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(facilityId))
                work.FacilityId = facilityId;
        }
    }

    public void Claim(Guid runId, IEnumerable<string> resourcePaths)
    {
        ArgumentNullException.ThrowIfNull(resourcePaths);
        lock (_gate)
        {
            var work = WorkFor(runId);
            foreach (var path in resourcePaths)
            {
                if (!string.IsNullOrWhiteSpace(path))
                    work.Paths.Add(path);
            }
        }
    }

    public void Release(Guid runId)
    {
        lock (_gate)
            _runs.Remove(runId);
    }

    public IReadOnlySet<string> PathsHeldByOthers(Guid runId)
    {
        lock (_gate)
        {
            var held = new HashSet<string>(StringComparer.Ordinal);
            if (!_runs.TryGetValue(runId, out var self))
                return held;

            foreach (var (id, work) in _runs)
            {
                if (id == runId)
                    continue;
                if (!string.Equals(work.EhrKey, self.EhrKey, StringComparison.Ordinal))
                    continue;
                held.UnionWith(work.Paths);
            }

            return held;
        }
    }

    /// <summary>
    /// Facilities another ongoing run on this same endpoint still holds.
    /// A run on a different endpoint does not block cleanup here.
    /// </summary>
    public IReadOnlyCollection<string> FacilitiesHeldByOthers(Guid runId)
    {
        lock (_gate)
        {
            var facilities = new HashSet<string>(StringComparer.Ordinal);
            if (!_runs.TryGetValue(runId, out var self))
                return facilities;

            foreach (var (id, work) in _runs)
            {
                if (id == runId || string.IsNullOrWhiteSpace(work.FacilityId))
                    continue;
                if (!string.Equals(work.EhrKey, self.EhrKey, StringComparison.Ordinal))
                    continue;
                facilities.Add(work.FacilityId);
            }

            return facilities;
        }
    }

    private Work WorkFor(Guid runId)
    {
        if (!_runs.TryGetValue(runId, out var work))
        {
            work = new Work();
            _runs[runId] = work;
        }

        return work;
    }

    private sealed class Work
    {
        public string EhrKey { get; set; } = string.Empty;
        public string FacilityId { get; set; } = string.Empty;
        public HashSet<string> Paths { get; } = new(StringComparer.Ordinal);
    }
}
