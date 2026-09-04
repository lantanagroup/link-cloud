using System.Text.RegularExpressions;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;

// Inverse of LocationOrgFhirPathBuilder. Round-trips anything this BFF wrote; an unrecognized condition falls back to custom-fhir-path.
internal static class LocationOrgFhirPathParser
{
    private static readonly Regex ManagingOrgPattern = new(
        @"^Location\.managingOrganization\.reference = '(?:Organization/)?(?<id>[^']*)'$",
        RegexOptions.Compiled);

    private static readonly Regex LocationTypePattern = new(
        @"^Location\.type\.coding\.where\(code = '(?<code>[^']*)'\)\.exists\(\) and Location\.alias\.contains\('(?<alias>[^']*)'\)$",
        RegexOptions.Compiled);

    private static readonly Regex LocationIdentifierPattern = new(
        @"^Location\.identifier\.where\(system = '(?<system>[^']*)' and value = '(?<code>[^']*)'\)\.exists\(\)$",
        RegexOptions.Compiled);

    public static LocationOrgSection Parse(IReadOnlyList<string> fhirPaths)
    {
        var ordered = fhirPaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        if (ordered.Count == 0)
        {
            return new LocationOrgSection();
        }

        if (ordered.All(path => ManagingOrgPattern.IsMatch(path)))
        {
            return new LocationOrgSection
            {
                Method = "managing-org",
                ManagingOrganizationIds = ordered.Select(path => ManagingOrgPattern.Match(path).Groups["id"].Value).ToList()
            };
        }

        if (ordered.All(path => LocationTypePattern.IsMatch(path)))
        {
            return new LocationOrgSection
            {
                Method = "location-type",
                LocationTypes = ordered.Select(path =>
                {
                    var match = LocationTypePattern.Match(path);
                    return new LocationTypeEntry { Code = match.Groups["code"].Value, Alias = match.Groups["alias"].Value };
                }).ToList()
            };
        }

        if (ordered.All(path => LocationIdentifierPattern.IsMatch(path)))
        {
            return new LocationOrgSection
            {
                Method = "location-identifier",
                LocationIdentifiers = ordered.Select(path =>
                {
                    var match = LocationIdentifierPattern.Match(path);
                    return new LocationIdentifierEntry { System = match.Groups["system"].Value, Code = match.Groups["code"].Value };
                }).ToList()
            };
        }

        // Not one of our own shapes - show the first condition verbatim.
        return new LocationOrgSection
        {
            Method = "custom-fhir-path",
            CustomFhirPath = ordered[0]
        };
    }
}
