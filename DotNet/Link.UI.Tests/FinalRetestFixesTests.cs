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
    public void Reports_measures_column_flexes_so_the_table_fits_at_1366()
    {
        var view = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Reports", "Index.cshtml"));
        var css = File.ReadAllText(Path.Combine(Root(), "Link.UI", "wwwroot", "css", "site.css"));
        Assert.Contains("<th class=\"lu-measures-col lu-flex\">", view);
        Assert.Contains("<td class=\"lu-flex\">", view);
        Assert.Contains(".lu-reports-table td.lu-flex > .lu-measures { display: flex; width: 0; min-width: 100%; overflow: hidden; }", css);
    }

    [Fact]
    public void Partitions_tab_does_not_fetch_topic_configs_it_never_shows()
    {
        var controller = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Controllers", "OperationsController.cs"));
        Assert.DoesNotContain("GetConfigsAsync(", controller);
    }

    [Fact]
    public void Topic_configs_endpoint_turns_a_rejection_into_a_problem_instead_of_a_500()
    {
        var endpoints = File.ReadAllText(Path.Combine(Root(), "Admin.BFF", "Presentation", "Endpoints", "KafkaOpsEndpoints.cs"));
        var start = endpoints.IndexOf("Task<IResult> GetConfigs(", StringComparison.Ordinal);
        var body = endpoints.Substring(start, endpoints.IndexOf("Task<IResult> PlanMigration(", StringComparison.Ordinal) - start);
        Assert.Contains("catch (KafkaOpsRejectedException ex)", body);
        Assert.Contains("return StatusFor(ex);", body);
    }
}
