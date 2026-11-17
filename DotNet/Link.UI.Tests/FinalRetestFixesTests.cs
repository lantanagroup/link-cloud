using System.IO;
using Xunit;

namespace Link.UI.Tests;

public class FinalRetestFixesTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "DotNet");
    }

    [Fact]
    public void Reports_table_moves_measures_to_the_report_page_and_merges_census_and_ip()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Reports", "Index.cshtml"));
        var detail = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Tenants", "_ReportBody.cshtml"));
        Assert.DoesNotContain(">Measures</th>", view);
        Assert.Contains("<th class=\"text-end text-nowrap\">Census / IP</th>", view);
        Assert.Contains("<span data-census=\"@report.Id\">", view);
        Assert.Contains("<dt class=\"col-sm-4\">Measures</dt>", detail);
    }
}
