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
        var detail = File.ReadAllText(Path.Combine(Root(), "Link.UI", "Views", "Tenants", "Report.cshtml"));
        Assert.DoesNotContain(">Measures</th>", view);
        Assert.Contains("<th class=\"text-end text-nowrap\">Census / IP</th>", view);
        Assert.Contains("<span data-census=\"@report.Id\">", view);
        Assert.Contains("<dt class=\"col-sm-3\">Measures</dt>", detail);
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
