using Automation.UI.Models;
using Hl7.Fhir.Model;

namespace Automation.UI.Services.ConfigurationGeneration;

public static class UploadedBundleAnalyzer
{
    public static BundleConfigFingerprint Analyze(IEnumerable<Resource> resources)
    {
        var fp = new BundleConfigFingerprint();
        foreach (var resource in resources.Where(r => r != null))
        {
            var type = resource.TypeName;
            fp.ResourceCounts[type] = fp.ResourceCounts.TryGetValue(type, out var c) ? c + 1 : 1;

            if (resource is Patient)
                fp.PatientCount++;

            if (resource is Location location)
                AddLocation(fp, location);

            CollectExtensions(fp, resource, type);
            CollectCodings(fp, resource, type);
        }

        return fp;
    }

    public static BundleConfigFingerprint Merge(BundleConfigFingerprint left, BundleConfigFingerprint? right)
    {
        if (right == null)
            return Clone(left);

        var merged = Clone(left);
        foreach (var (type, count) in right.ResourceCounts)
            merged.ResourceCounts[type] = merged.ResourceCounts.TryGetValue(type, out var c) ? c + count : count;

        merged.PatientCount += right.PatientCount;
        merged.LocationCount += right.LocationCount;
        merged.LocationsWithoutIdentifier += right.LocationsWithoutIdentifier;

        foreach (var id in right.LocationIdentifiers)
        {
            if (!merged.LocationIdentifiers.Any(x => Same(x.System, id.System) && Same(x.Value, id.Value)))
                merged.LocationIdentifiers.Add(id);
        }

        foreach (var t in right.LocationTypes)
        {
            if (!merged.LocationTypes.Any(x => Same(x.System, t.System) && Same(x.Code, t.Code)))
                merged.LocationTypes.Add(t);
        }

        foreach (var alias in right.LocationAliases)
        {
            if (!merged.LocationAliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                merged.LocationAliases.Add(alias);
        }

        foreach (var ext in right.Extensions)
        {
            if (!merged.Extensions.Any(x => Same(x.Url, ext.Url) && Same(x.ResourceType, ext.ResourceType)))
                merged.Extensions.Add(ext);
        }

        foreach (var coding in right.Codings)
        {
            if (!merged.Codings.Any(x =>
                    Same(x.ResourceType, coding.ResourceType)
                    && Same(x.Path, coding.Path)
                    && Same(x.System, coding.System)
                    && Same(x.Code, coding.Code)))
            {
                merged.Codings.Add(coding);
            }
        }

        return merged;
    }

    public static BundleConfigFingerprint Clone(BundleConfigFingerprint source)
        => new()
        {
            ResourceCounts = new Dictionary<string, int>(source.ResourceCounts, StringComparer.OrdinalIgnoreCase),
            LocationIdentifiers = [.. source.LocationIdentifiers],
            LocationTypes = [.. source.LocationTypes],
            LocationAliases = [.. source.LocationAliases],
            Extensions = [.. source.Extensions],
            Codings = [.. source.Codings],
            LocationCount = source.LocationCount,
            PatientCount = source.PatientCount,
            LocationsWithoutIdentifier = source.LocationsWithoutIdentifier
        };

    private static void AddLocation(BundleConfigFingerprint fp, Location location)
    {
        fp.LocationCount++;
        var hasUsableIdentifier = false;
        foreach (var identifier in location.Identifier ?? [])
        {
            var system = identifier.System?.Trim() ?? "";
            var value = identifier.Value?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(system) && string.IsNullOrWhiteSpace(value))
                continue;
            hasUsableIdentifier = true;
            if (fp.LocationIdentifiers.Any(x => Same(x.System, system) && Same(x.Value, value)))
                continue;
            fp.LocationIdentifiers.Add(new LocationIdentifierHint { System = system, Value = value });
        }
        if (!hasUsableIdentifier)
            fp.LocationsWithoutIdentifier++;

        foreach (var type in location.Type ?? [])
        {
            foreach (var coding in type.Coding ?? [])
            {
                var system = coding.System?.Trim() ?? "";
                var code = coding.Code?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(system) && string.IsNullOrWhiteSpace(code))
                    continue;
                if (fp.LocationTypes.Any(x => Same(x.System, system) && Same(x.Code, code)))
                    continue;
                fp.LocationTypes.Add(new LocationTypeHint { System = system, Code = code });
            }
        }

        foreach (var alias in location.Alias ?? [])
        {
            var value = alias?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (!fp.LocationAliases.Contains(value, StringComparer.OrdinalIgnoreCase))
                fp.LocationAliases.Add(value);
        }
    }

    private static void CollectExtensions(BundleConfigFingerprint fp, Resource resource, string resourceType)
    {
        if (resource is not DomainResource domain)
            return;

        foreach (var ext in (domain.Extension ?? []).Concat(domain.ModifierExtension ?? []))
            AddExtension(fp, resourceType, ext);
    }

    private static void AddExtension(BundleConfigFingerprint fp, string resourceType, Extension? extension)
    {
        if (extension == null)
            return;
        var url = extension.Url?.Trim();
        if (IsAbsoluteExtensionUrl(url)
            && !fp.Extensions.Any(x => Same(x.Url, url) && Same(x.ResourceType, resourceType)))
        {
            fp.Extensions.Add(new ExtensionHint { Url = url!, ResourceType = resourceType });
        }

        foreach (var nested in extension.Extension ?? [])
            AddExtension(fp, resourceType, nested);
    }

    private static void CollectCodings(BundleConfigFingerprint fp, Resource resource, string resourceType)
    {
        switch (resource)
        {
            case Location location:
                foreach (var type in location.Type ?? [])
                    AddCodings(fp, resourceType, "type.coding", type.Coding);
                break;
            case Encounter encounter:
                AddCodings(fp, resourceType, "class", encounter.Class == null ? null : [encounter.Class]);
                break;
            case Condition condition:
                AddCodings(fp, resourceType, "code.coding", condition.Code?.Coding);
                break;
            case Observation observation:
                AddCodings(fp, resourceType, "code.coding", observation.Code?.Coding);
                break;
        }
    }

    private static void AddCodings(
        BundleConfigFingerprint fp,
        string resourceType,
        string path,
        IEnumerable<Coding>? codings)
    {
        foreach (var coding in codings ?? [])
        {
            var system = coding.System?.Trim() ?? "";
            var code = coding.Code?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(system) || string.IsNullOrWhiteSpace(code))
                continue;
            if (fp.Codings.Any(x =>
                    Same(x.ResourceType, resourceType)
                    && Same(x.Path, path)
                    && Same(x.System, system)
                    && Same(x.Code, code)))
            {
                continue;
            }

            fp.Codings.Add(new CodingHint
            {
                ResourceType = resourceType,
                Path = path,
                System = system,
                Code = code,
                Display = coding.Display
            });
        }
    }

    public static bool IsAbsoluteExtensionUrl(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && url == url.Trim()
           && Uri.TryCreate(url, UriKind.Absolute, out _);

    private static bool Same(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
