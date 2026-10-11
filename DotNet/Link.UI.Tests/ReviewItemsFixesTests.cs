using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ReviewItemsFixesTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), "Link.UI", .. parts]));

    [Theory]
    [InlineData(0, "0 brokers")]
    [InlineData(1, "1 broker")]
    [InlineData(2, "2 brokers")]
    [InlineData(1500, "1,500 brokers")]
    public void Counts_pluralize(long count, string expected) => Assert.Equal(expected, Counts.Of(count, "broker"));

    [Fact]
    public void Resubmit_and_regenerate_are_execute_buttons()
    {
        foreach (var view in new[] { Read("Views", "Reports", "Index.cshtml"), Read("Views", "Tenants", "_ViewReports.cshtml") })
        {
            Assert.Contains("class=\"btn btn-sm btn-au-execute lu-icon-btn\" data-resubmit-open", view);
            Assert.Contains("<button type=\"submit\" class=\"btn btn-au-execute\" name=\"bypassSubmission\"", view);
            Assert.DoesNotContain("btn-warning\">Resubmit", view);
            Assert.Contains("<td class=\"lu-created\" data-lu-time-compact>", view);
        }
    }

    [Fact]
    public void Compact_times_and_trend_axis_label()
    {
        Assert.Contains("[data-lu-time-compact]", Read("wwwroot", "js", "live-region.js"));
        var overview = Read("Views", "Home", "_Overview.cshtml");
        Assert.DoesNotContain(">UTC days<", overview);
        Assert.Contains("home-trend-axis", overview);
        Assert.Contains("@Counts.Of(Model.BrokerCount, \"broker\")", Read("Views", "Operations", "_Brokers.cshtml"));
    }

    [Fact]
    public void Row_actions_are_icon_only_with_tooltips_and_delete_is_named_delete()
    {
        var js = Read("wwwroot", "js", "row-icons.js");
        Assert.Contains("setAttribute(\"title\", label)", js);
        Assert.Contains("setAttribute(\"aria-label\", label)", js);
        Assert.Contains("<script src=\"~/js/row-icons.js\"", Read("Views", "Shared", "_Layout.cshtml"));
        Assert.Contains("td .btn.btn-sm.lu-row-icon > :not(i.bi) { display: none; }", Read("wwwroot", "css", "site.css"));
        foreach (var view in new[] { Read("Views", "Reports", "Index.cshtml"), Read("Views", "Tenants", "_ViewReports.cshtml") })
        {
            Assert.DoesNotContain(">Clean up<", view);
            Assert.Contains("title=\"Delete\"", view);
            Assert.Contains("onsubmit=\"return confirm(", view);
        }
    }

    [Fact]
    public void Navigating_row_actions_use_the_go_to_page_icon_and_modal_actions_keep_the_eye()
    {
        Assert.Contains("bi-box-arrow-up-right\" aria-hidden=\"true\"></i><span class=\"ms-1\">View</span></a>", Read("Views", "Tenants", "Facility.cshtml"));
        Assert.Contains("title=\"Open record\"", Read("Views", "Operations", "_Messages.cshtml"));
        Assert.Contains("title=\"Open group\"", Read("Views", "Operations", "_TopicConsumers.cshtml"));
        Assert.Contains("cleanup-open-report\" data-report-id=\"@report.Id\" title=\"View\"", Read("Views", "Cleanup", "Index.cshtml"));
        Assert.Contains("bi-eye", Read("Views", "Cleanup", "Index.cshtml"));
    }
}