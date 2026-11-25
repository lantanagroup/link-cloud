using System.Text.RegularExpressions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>Resource donuts use Automation.UI's chart colours and carry a colour-matched bar graph.</summary>
public class ResourceDonutPaletteTests
{
    private static readonly string[] Greys = ["#6c757d", "#343a40", "#adb5bd", "#000000", "#212529", "#495057"];

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("DotNet root not found.");
    }

    private static string[] Hexes(string text) =>
        Regex.Matches(text, "#[0-9a-fA-F]{6}").Select(m => m.Value.ToLowerInvariant()).ToArray();

    [Fact]
    public void Donut_colours_are_Automation_UI_chart_colours_without_greys_in_the_same_order()
    {
        var manifest = File.ReadAllText(Path.Combine(Root(), "Automation.UI", "Views", "Runs", "Manifest.cshtml"));
        var block = Regex.Match(manifest, @"const chartColors = \[([^\]]+)\]").Groups[1].Value;
        var expected = Hexes(block).Where(hex => !Greys.Contains(hex)).ToArray();
        Assert.Equal(11, expected.Length);
        Assert.Equal(expected, ReportManifestRules.DonutColors.Select(c => c.ToLowerInvariant()).ToArray());
    }

    [Fact]
    public void Chart_palette_category_list_matches_the_donut_colours_and_has_no_black_or_dark_grey()
    {
        var js = File.ReadAllText(Path.Combine(Root(), "Link.UI", "wwwroot", "js", "chart-palette.js"));
        var category = Hexes(Regex.Match(js, @"var category = \[([^\]]+)\]").Groups[1].Value.Replace("success", "#28a745"));
        Assert.Equal(ReportManifestRules.DonutColors.Select(c => c.ToLowerInvariant()), category);
        foreach (var hex in Hexes(js).Concat(ReportManifestRules.DonutColors))
            Assert.DoesNotContain(hex.ToLowerInvariant(), new[] { "#000000", "#212529", "#343a40", "#495057", "#6c757d" });
    }

    [Fact]
    public void Overview_and_patient_modal_render_colour_matched_bars_biggest_first()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Shared", "_ReportManifest.cshtml"));
        Assert.Contains("lu-donut-legend lu-donut-bars", view);
        Assert.Contains("OrderByDescending(i => donutRows[i].Total)", view);
        Assert.Contains("background:@SliceColor(index)\"></span></span>", view);
        Assert.Contains("legend.className = \"lu-donut-legend lu-donut-bars\"", view);
        Assert.Contains("return right.total - left.total;", view);
        Assert.Contains("fill.style.background = row.color;", view);
        var css = File.ReadAllText(Path.Combine(Root(), "Link.UI", "wwwroot", "css", "site.css"));
        Assert.Contains(".lu-donut-legend.lu-donut-bars", css);
    }
}