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
        var flex = Regex.Match(css, @"td:has\(\.lu-labeled-id\):not\(:has\(\.btn\)\)[\s\S]*?\{(?<body>[^}]*)\}");
        flex.Success.Should().BeTrue();
        flex.Groups["body"].Value.Should().Contain("max-width: 0");
        flex.Groups["body"].Value.Should().Contain("text-overflow: ellipsis");
        flex.Groups["body"].Value.Should().NotContain("12rem");
        flex.Groups["body"].Value.Should().NotContain("20ch");
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
