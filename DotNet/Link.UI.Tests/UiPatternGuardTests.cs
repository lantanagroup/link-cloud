using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// Scans Link.UI views, scripts, and the shared stylesheet for patterns that
/// the shell is not allowed to grow back.
/// </summary>
public class UiPatternGuardTests
{
    private static readonly string[] ForbiddenHex =
    [
        "#0d6efd",
        "#0dcaf0",
        "#4da3ff",
        "#198754",
        "#084298",
        "#e8f3ff",
        "#2b86e6"
    ];

    private static readonly string[] ForbiddenMarkup =
    [
        "bg-primary",
        "text-primary",
        "btn-primary",
        "btn-outline-",
        "link-primary",
        "text-info",
        "bg-info",
        "nav-tabs"
    ];

    [Fact]
    public void Views_and_scripts_do_not_use_blue_or_the_old_green()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Concat(ProductFiles("*.js", Path.Combine("wwwroot", "js")))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => ForbiddenHex.Any(token => row.line.Contains(token, StringComparison.OrdinalIgnoreCase))
                    || ForbiddenMarkup.Any(token => row.line.Contains(token, StringComparison.Ordinal))
                        && !row.line.Contains("patient-sort-button", StringComparison.Ordinal))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Shared_stylesheet_does_not_hard_code_blue_or_the_old_green()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        foreach (var token in ForbiddenHex)
            css.Contains(token, StringComparison.OrdinalIgnoreCase).Should().BeFalse(token);

        css.Should().Contain("--au-success:     #28a745;");
        css.Should().Contain("--au-accent:      #343a40;");
        css.Should().Contain(".lu-chart");
        css.Should().Contain(".lu-chart-donut");
        css.Should().Contain(".lu-logs-table");
        css.Should().Contain("min-width: 72rem");
        css.Should().Contain(".lu-reports-table");
        css.Should().Contain(".btn-check:checked + .btn-au-neutral");
    }

    [Fact]
    public void Back_controls_use_the_shared_partial()
    {
        var hits = ProductFiles("*.cshtml", "Views")
            .Where(file => !file.EndsWith("_BackButton.cshtml", StringComparison.OrdinalIgnoreCase))
            .SelectMany(file => File.ReadAllLines(file)
                .Select((line, index) => (file, line, index))
                .Where(row => row.line.Contains("bi-arrow-left", StringComparison.Ordinal)
                    && !row.line.Contains("bi-arrow-left-right", StringComparison.Ordinal))
                .Select(row => Rel(row.file) + ":" + (row.index + 1)))
            .ToList();

        hits.Should().BeEmpty();
    }

    [Fact]
    public void Tab_strips_use_the_shared_bar()
    {
        var run = File.ReadAllText(Path.Combine(Root(), "Views", "Automation", "_RunDetail.cshtml"));
        var manifest = File.ReadAllText(Path.Combine(Root(), "Views", "Automation", "Manifest.cshtml"));
        var normalizations = File.ReadAllText(Path.Combine(Root(), "Views", "Normalizations", "Index.cshtml"));
        var plans = File.ReadAllText(Path.Combine(Root(), "Views", "QueryPlans", "Index.cshtml"));
        foreach (var view in new[] { run, manifest, normalizations, plans })
        {
            view.Should().Contain("lu-section-nav");
            view.Should().NotContain("nav-tabs");
        }
    }

    [Fact]
    public void Acquisition_logs_keep_the_shared_actions()
    {
        var list = File.ReadAllText(Path.Combine(Root(), "Views", "Logs", "_AcquisitionLogList.cshtml"));
        list.Should().Contain("StatusPills.ForLog");
        list.Should().Contain("ProcessOne");
        list.Should().Contain("ReturnUrlRules.Sanitize");
        list.Should().Contain("lu-logs-table");
        list.Should().NotContain("<ul");
    }

    [Fact]
    public void Log_and_report_tables_truncate()
    {
        File.ReadAllText(Path.Combine(Root(), "Views", "Logs", "_AcquisitionLogList.cshtml"))
            .Should().Contain("lu-col-created");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Index.cshtml"))
            .Should().Contain("lu-reports-table");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Index.cshtml"))
            .Should().Contain("lu-measures");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Validation.cshtml"))
            .Should().Contain("lu-issue-filters");
        File.ReadAllText(Path.Combine(Root(), "Views", "Reports", "Validation.cshtml"))
            .Should().Contain("col-category");
    }

    private static IEnumerable<string> ProductFiles(string pattern, string relative)
    {
        var dir = Path.Combine(Root(), relative);
        return Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }

    private static string Rel(string path) => Path.GetRelativePath(Root(), path);
}
