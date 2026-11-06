using System.IO;
using Xunit;

namespace Link.UI.Tests;

public class LastIssuesFixesTests
{
    private static string Dot(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "DotNet" }.Concat(parts).ToArray()));
    }

    private static string Rule(string css, string selector)
    {
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, selector);
        return css.Substring(start, css.IndexOf('}', start) - start);
    }

    [Fact]
    public void Dashboard_bars_use_the_status_palette_not_black()
    {
        var bar = Rule(Dot("Link.UI", "wwwroot", "css", "site.css"), ".home-trend-bar");
        Assert.Contains("var(--au-success)", bar);
        Assert.DoesNotContain("accent-fill", bar);
    }

    [Fact]
    public void Copy_icon_sits_inside_the_cell_without_layout_space()
    {
        var css = Dot("Link.UI", "wwwroot", "css", "site.css");
        Assert.DoesNotContain("transform: translateY(-55%);", css); Assert.Contains("lu-copy-after", css); Assert.Contains("lu-copy-after", Dot("Link.UI", "wwwroot", "js", "cell-copy.js")); Assert.DoesNotContain("td.lu-copy-host {\n    padding-right", css.Replace("\r\n", "\n"));
        Assert.Contains("@media (hover: hover) and (pointer: fine) {", css);
    }

    [Fact]
    public void Normalization_default_suite_retries_write_conflicts()
    {
        var source = Dot("Link.UI", "Engine", "Services", "Persistence", "MongoNormalizationStore.cs");
        var body = source.Substring(source.IndexOf("SetDefaultSuiteAsync(Guid id", StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("DeleteSuiteAsync", StringComparison.Ordinal));
        Assert.Contains("WithTransactionAsync", body);
        Assert.DoesNotContain("StartTransaction()", body);
    }

    [Fact]
    public void No_table_column_is_hidden_at_laptop_width()
    {
        var css = Dot("Link.UI", "wwwroot", "css", "site.css");
        foreach (System.Text.RegularExpressions.Match rule in System.Text.RegularExpressions.Regex.Matches(css, @"([^{}]*)\{([^{}]*)\}"))
        {
            var selector = rule.Groups[1].Value;
            var body = rule.Groups[2].Value;
            if (body.Contains("display: none") || body.Contains("display:none"))
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(selector, @"\b(th|td)(:nth-child|:nth-of-type|\.)"), "Column-hiding rule: " + selector.Trim());
        }
        Assert.Contains("<td class=\"lu-created\" data-lu-time-compact>@report.Created</td>", Dot("Link.UI", "Views", "Reports", "Index.cshtml"));
        Assert.Contains("<td class=\"lu-created\" data-lu-time-compact>@report.Created</td>", Dot("Link.UI", "Views", "Tenants", "_ViewReports.cshtml"));
    }

    [Fact]
    public void Wide_tables_move_secondary_columns_to_their_detail_views()
    {
        var facility = Dot("Link.UI", "Views", "Tenants", "_ViewReports.cshtml");
        Assert.DoesNotContain(">Measures</th>", facility);
        Assert.Contains("Census / IP", facility);
        Assert.Contains(".lu-reports-table > tbody > tr > td:nth-child(2) .lu-clip { max-width: none; }", Dot("Link.UI", "wwwroot", "css", "site.css"));
        Assert.Contains("<td class=\"lu-wrap\">@row.Name</td>", Dot("Link.UI", "Views", "Configuration", "Operations.cshtml"));
        Assert.Contains("<td class=\"lu-wrap\">@row.LongDescription</td>", Dot("Link.UI", "Views", "Configuration", "Hsloc.cshtml"));
    }
}
