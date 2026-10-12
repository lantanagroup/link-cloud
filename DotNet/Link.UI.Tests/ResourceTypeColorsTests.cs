using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>Nick: never repeat colours. Every resource type gets its own colour; grey is only for "Other".</summary>
public class ResourceTypeColorsTests
{
    // Types the Automation generator emits (Location ... CarePlan, List) plus MeasureReport from the pipeline.
    private static readonly string[] Generated =
    [
        "Location", "Patient", "Encounter", "Practitioner", "DiagnosticReport", "Medication", "Organization", "Device",
        "Specimen", "Condition", "MedicationRequest", "MedicationAdministration", "ServiceRequest", "Procedure",
        "CareTeam", "Observation", "Coverage", "CarePlan", "List", "MeasureReport"
    ];

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("DotNet root not found.");
    }

    private static (double L, double A, double B) Lab(string hex)
    {
        double Lin(int i)
        {
            var c = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        double r = Lin(1), g = Lin(3), b = Lin(5);
        double x = (r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047, y = r * 0.2126 + g * 0.7152 + b * 0.0722, z = (r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883;
        static double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16 / 116.0;
        return (116 * F(y) - 16, 500 * (F(x) - F(y)), 200 * (F(y) - F(z)));
    }

    private static double DeltaE(string a, string b)
    {
        var p = Lab(a);
        var q = Lab(b);
        return Math.Sqrt(Math.Pow(p.L - q.L, 2) + Math.Pow(p.A - q.A, 2) + Math.Pow(p.B - q.B, 2));
    }

    [Fact]
    public void No_two_types_share_a_colour_and_none_uses_the_Other_grey()
    {
        var colors = ResourceTypeColors.Map.Values.Select(c => c.ToLowerInvariant()).ToList();
        Assert.True(ResourceTypeColors.Map.Count >= 40, "cover at least the ~40 FHIR types we generate or see");
        Assert.Equal(colors.Count, colors.Distinct().Count());
        Assert.DoesNotContain(ResourceTypeColors.Other, colors);
        Assert.Equal(ResourceTypeColors.Other, ResourceTypeColors.For("Other"));
    }

    [Fact]
    public void Colours_are_bright_with_no_black_grey_or_near_duplicates()
    {
        var colors = ResourceTypeColors.Map.Values.ToList();
        foreach (var color in colors)
        {
            var lab = Lab(color);
            var chroma = Math.Sqrt(lab.A * lab.A + lab.B * lab.B);
            // No black or dark grey: deep saturated hues (Automation.UI's purples) are fine, near-black and greys are not.
            Assert.True(lab.L >= 25, color + " is near black");
            Assert.True(chroma >= 30, color + " reads as grey");
            Assert.True(lab.L >= 40 || chroma >= 60, color + " is a dark muted colour");
            Assert.True(DeltaE(color, ResourceTypeColors.Other) >= 25, color + " is too close to the Other grey");
        }
        for (var i = 0; i < colors.Count; i++)
            for (var j = i + 1; j < colors.Count; j++)
                Assert.True(DeltaE(colors[i], colors[j]) >= 12, colors[i] + " and " + colors[j] + " are near-duplicates");
    }

    [Fact]
    public void Starts_with_Automation_UI_colours_and_covers_every_generated_type()
    {
        Assert.Equal(ReportManifestRules.DonutColors, ResourceTypeColors.Map.Values.Take(ReportManifestRules.DonutColors.Length));
        foreach (var type in Generated)
            Assert.True(ResourceTypeColors.Map.ContainsKey(type), type + " has no fixed colour");
    }

    [Fact]
    public void Unlisted_types_get_a_stable_non_grey_colour_and_the_script_mirrors_the_rule()
    {
        var first = ResourceTypeColors.For("SomeFutureType");
        Assert.Equal(first, ResourceTypeColors.For("somefuturetype"));
        var lab = Lab(first);
        Assert.True(Math.Sqrt(lab.A * lab.A + lab.B * lab.B) >= 30);
        Assert.Equal(ResourceTypeColors.Map["Observation"], ResourceTypeColors.For(" observation "));

        var js = File.ReadAllText(Path.Combine(Root(), "Link.UI", "wwwroot", "js", "chart-palette.js"));
        Assert.Contains("hash = 2166136261", js);
        Assert.Contains("16777619", js);
        Assert.Contains("hslHex(hash % 360, 72, 52)", js);
        Assert.Contains("window.luTypeColors", File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Shared", "_Layout.cshtml")));
    }

    [Fact]
    public void Every_resource_chart_colours_by_type_not_by_position()
    {
        var graph = File.ReadAllText(Path.Combine(Root(), "Link.UI", "wwwroot", "js", "resource-graph.js"));
        Assert.Contains("color: typeColorOf(type.name)", graph);
        Assert.Contains("fill.style.background = typeColorOf(group.name);", graph);
        Assert.DoesNotContain("colors[index % colors.length]", graph);
        var view = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Shared", "_ReportManifest.cshtml"));
        Assert.Contains("ResourceTypeColors.For(donutRows[index].Name)", view);
        Assert.Contains("window.luChartPalette.typeColor(row.name)", view);
    }
}