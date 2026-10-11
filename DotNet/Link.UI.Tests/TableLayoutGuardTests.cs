using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class TableLayoutGuardTests
{
    [Fact]
    public void Copy_button_does_not_change_the_cell_box()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        var host = Regex.Match(css, @"td\.lu-copy-host\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        host.Success.Should().BeTrue();
        host.Groups["body"].Value.Should().Contain("position: relative");
        host.Groups["body"].Value.Should().NotContain("padding");
        host.Groups["body"].Value.Should().NotContain("width");

        var button = Regex.Match(css, @"\.lu-cell-copy\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        button.Success.Should().BeTrue();
        var body = button.Groups["body"].Value;
        body.Should().Contain("position: absolute");
        body.Should().Contain("opacity: 0");
        body.Should().Contain("visibility: hidden");
        body.Should().NotContain("display:");

        var shown = css.IndexOf("td.lu-copy-host > .btn.lu-copy.lu-cell-copy:focus", StringComparison.Ordinal);
        shown.Should().BeGreaterThan(0);
        var shownRule = css[shown..css.IndexOf('}', shown)];
        shownRule.Should().Contain("opacity: 1");
        shownRule.Should().Contain("visibility: visible");
        shownRule.Should().NotContain("display:");
        shownRule.Should().NotContain("width:");
        shownRule.Should().NotContain("padding");
    }

    [Fact]
    public void Tables_use_auto_layout_and_do_not_scroll_sideways()
    {
        var css = File.ReadAllText(Path.Combine(Root(), "wwwroot", "css", "site.css"));
        css.Should().Contain("table-layout: auto");
        css.Should().NotContain("table-layout: fixed");
        css.Should().Contain("overflow-x: clip");
        css.Should().Contain("text-overflow: ellipsis");
        css.Should().Contain("width: 1%");
        css.Should().Contain("white-space: nowrap");
        css.Should().NotContain("min-width: 72rem");
        css.Should().NotContain("min-width: 48rem");
        css.Should().Contain("#recentRunsCard .table-responsive");
        css.Should().Contain("table .lu-clip");
        css.Should().Contain("max-width: 100%");
        var flex = Regex.Match(css, @"td:has\(\.lu-labeled-id\):not\(:has\(\.btn:not\(\.lu-cell-copy\)\)\)[\s\S]*?\{(?<body>[^}]*)\}");
        flex.Success.Should().BeTrue();
        flex.Groups["body"].Value.Should().Contain("max-width: 0");
        flex.Groups["body"].Value.Should().Contain("text-overflow: ellipsis");
        flex.Groups["body"].Value.Should().NotContain("12rem");
        flex.Groups["body"].Value.Should().NotContain("20ch");
    }

    [Fact]
    public void A_named_long_column_takes_the_space_and_the_leading_cell_shrinks()
    {
        var root = Root();
        var css = File.ReadAllText(Path.Combine(root, "wwwroot", "css", "site.css"));
        var flex = Regex.Match(css, @"table\.table td\.lu-flex,[^{]*\{(?<body>[^}]*)\}");
        flex.Success.Should().BeTrue();
        flex.Groups["body"].Value.Should().Contain("max-width: 0");
        flex.Groups["body"].Value.Should().Contain("text-overflow: ellipsis");
        var lead = Regex.Match(css, @"tr > \.lu-shrink:is\(th, td\)[^{]*\{(?<body>[^}]*)\}");
        lead.Success.Should().BeTrue();
        lead.Groups["body"].Value.Should().Contain("width: 1%");
        lead.Groups["body"].Value.Should().Contain("white-space: nowrap");
        var wrap = Regex.Match(css, @"table\.table td\.lu-wrap,[^{]*\{(?<body>[^}]*)\}");
        wrap.Success.Should().BeTrue();
        wrap.Groups["body"].Value.Should().Contain("overflow-wrap: break-word");
        css.Should().Contain("td:has(.btn:not(.lu-cell-copy))", "the hover copy button must not turn a value cell into an action cell");
        css.Should().NotContain("td:has(.btn),");
        var ops = Path.Combine(root, "Views", "Operations");
        File.ReadAllText(Path.Combine(ops, "_Messages.cshtml")).Should().Contain("<th class=\"lu-shrink\">Partition</th>");
        File.ReadAllText(Path.Combine(ops, "_Messages.cshtml")).Should().Contain("<th class=\"lu-flex\">Summary</th>");
        File.ReadAllText(Path.Combine(ops, "_Brokers.cshtml")).Should().Contain("<th class=\"lu-flex\">Host</th>");
        File.ReadAllText(Path.Combine(ops, "_Topic.cshtml")).Should().Contain("<th class=\"lu-wrap\">End offset</th>");
    }

    [Fact]
    public void Kafka_tabs_have_no_select_a_row_hint_boxes()
    {
        var ops = Path.Combine(Root(), "Views", "Operations");
        File.ReadAllText(Path.Combine(ops, "_Consumers.cshtml")).Should().NotContain("Select a group to add or remove replicas.");
        var brokers = File.ReadAllText(Path.Combine(ops, "_Brokers.cshtml"));
        brokers.Should().NotContain("Select a broker to preview");
        brokers.Should().NotContain("<div class=\"alert alert-secondary\" role=\"status\">@Model.Cluster.LogDirDetail</div>");
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI");
    }
}
