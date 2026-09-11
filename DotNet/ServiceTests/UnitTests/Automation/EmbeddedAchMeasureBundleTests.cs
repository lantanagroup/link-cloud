using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class EmbeddedAchMeasureBundleTests
{
    [Theory]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)]
    [InlineData(ProfiledMeasureType.NhsnAcuteCareHospitalDailyInitialPopulation)]
    public void Embedded_ach_bundles_include_every_cql_valueset(ProfiledMeasureType family)
    {
        using var doc = JsonDocument.Parse(ReadEmbeddedBundle(family));
        var vsUrls = new HashSet<string>(StringComparer.Ordinal);
        var cql = new StringBuilder();
        foreach (var entry in doc.RootElement.GetProperty("entry").EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource))
                continue;
            var type = resource.GetProperty("resourceType").GetString();
            if (type == "ValueSet" && resource.TryGetProperty("url", out var url))
                vsUrls.Add(url.GetString()!);
            if (type != "Library" || !resource.TryGetProperty("content", out var content))
                continue;
            foreach (var attachment in content.EnumerateArray())
            {
                var contentType = attachment.TryGetProperty("contentType", out var ct) ? ct.GetString() : null;
                if (contentType is null || !contentType.StartsWith("text/cql", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!attachment.TryGetProperty("data", out var data))
                    continue;
                cql.AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(data.GetString()!)));
            }
        }

        var missing = Regex.Matches(cql.ToString(), """valueset "[^"]+": '([^']+)'""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Where(u => !vsUrls.Contains(u))
            .ToList();

        missing.Should().BeEmpty("CQL valueset URLs must be present in the evaluation bundle");
    }

    private static string ReadEmbeddedBundle(ProfiledMeasureType family)
    {
        var location = ProfiledMeasureCatalog.GetBundleLocation(family);
        var resourceName = location["resource://".Length..];
        using var stream = typeof(ProfiledMeasureCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
