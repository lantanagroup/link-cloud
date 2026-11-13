using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class ManifestStickyHeaderTests
{
    [Fact]
    public void Report_manifest_tables_scroll_inside_the_existing_sticky_header_shell()
    {
        var view = File.ReadAllText(Repo("DotNet/Link.UI/Views/Shared/_ReportManifest.cshtml"));
        var css = File.ReadAllText(Repo("DotNet/Link.UI/wwwroot/css/site.css"));

        var staticTables = view.Split("<table class=\"au-table", StringSplitOptions.None).Length - 1;
        staticTables.Should().Be(6);
        Count(view, "<div class=\"table-responsive\">").Should().Be(5);
        Count(view, "<div class=\"table-responsive mb-3\">").Should().Be(1);

        var sticky = css.IndexOf(".au-table thead th,", StringComparison.Ordinal);
        sticky.Should().BeGreaterThan(0);
        var rule = css[sticky..css.IndexOf('}', sticky)];
        rule.Should().Contain("position: sticky");
        rule.Should().Contain("top: 0");
        rule.Should().Contain("z-index: 2");

        var shell = css.IndexOf(".au-card .table-responsive {", StringComparison.Ordinal);
        shell.Should().BeGreaterThan(0);
        var shellRule = css[shell..css.IndexOf('}', shell)];
        shellRule.Should().Contain("overflow-y: auto");
        shellRule.Should().Contain("max-height:");
    }

    private static int Count(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string Repo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
