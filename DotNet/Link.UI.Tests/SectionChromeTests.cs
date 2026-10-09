using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class SectionChromeTests
{
    [Fact]
    public void Section_tabs_share_one_black_bar()
    {
        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        var nav = css.IndexOf(".lu-section-nav {", StringComparison.Ordinal);
        nav.Should().BeGreaterThan(-1);
        var bar = css.Substring(nav, 280);
        bar.Should().Contain("background: var(--au-dark);");
        bar.Should().Contain("overflow-x: hidden");
        bar.Should().Contain("flex-wrap: wrap");

        var current = css.IndexOf(".lu-section-nav a.lu-nav-current", StringComparison.Ordinal);
        current.Should().BeGreaterThan(-1);
        var mark = css.Substring(current, 320);
        mark.Should().Contain("var(--au-warning)");
        mark.Should().NotContain("var(--au-accent)");
        mark.Should().NotContain("#4da3ff");

        foreach (var partial in new[]
        {
            "Views/Automation/_Nav.cshtml",
            "Views/Logs/_Nav.cshtml",
            "Views/Configuration/_Nav.cshtml",
            "Views/System/_Nav.cshtml",
            "Views/Shared/_ReportNav.cshtml"
        })
        {
            var text = File.ReadAllText(Path.Combine(ProjectRoot(), partial.Replace('/', Path.DirectorySeparatorChar)));
            text.Should().Contain("class=\"lu-section-nav\"");
            text.Should().Contain("bi bi-");
        }
    }

    [Fact]
    public void Open_menus_are_not_clipped_by_cards_or_accordions()
    {
        var css = File.ReadAllText(Path.Combine(ProjectRoot(), "wwwroot", "css", "site.css"));
        css.Should().NotContain("so header corners clip");

        var card = css.Substring(css.IndexOf(".au-card {", StringComparison.Ordinal), 220);
        card.Should().Contain("overflow: visible");

        var panels = css.Substring(css.IndexOf("#facilityPanels > .accordion-item {", StringComparison.Ordinal), 220);
        panels.Should().Contain("overflow: visible");

        var health = css.Substring(css.IndexOf(".au-health-accordion {", StringComparison.Ordinal), 180);
        health.Should().Contain("overflow: visible");

        css.Should().Contain(".dropdown-menu.show");
    }

    [Fact]
    public void Charts_share_a_fixed_compact_size()
    {
        var root = ProjectRoot();
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "site.css"));
        css.Should().Contain(".lu-chart");
        css.Should().Contain(".lu-chart-donut");
        var trend = css.Substring(css.IndexOf(".au-trend {", StringComparison.Ordinal), 120);
        trend.Should().Contain("height: 180px");
        trend.Should().Contain("max-height: 220px");

        var index = File.ReadAllText(Path.Combine(root, "Views", "Automation", "Index.cshtml"));
        index.Should().Contain("lu-chart-donut");
        index.Should().Contain("class=\"lu-chart\"");
        index.Should().NotContain("min-height:240px");

        var dashboard = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "automation-dashboard.js"));
        dashboard.Should().Contain("maintainAspectRatio: false");

        var run = File.ReadAllText(Path.Combine(root, "Views", "Automation", "_RunDetailScript.cshtml"));
        run.Should().Contain("maintainAspectRatio = false");
        run.Should().NotContain("maintainAspectRatio: true");

        var manifest = File.ReadAllText(Path.Combine(root, "Views", "Shared", "_ReportManifest.cshtml"));
        manifest.Should().Contain("lu-chart-donut");
        manifest.Should().Contain("class=\"lu-chart mb-3\"");
        manifest.Should().NotContain("maintainAspectRatio: true");
        manifest.Should().NotContain("max-height:320px");
    }

    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
