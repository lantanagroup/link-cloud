using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ReturnUrlRulesTests
{
    [Theory]
    [InlineData("https://evil.example/Reports")]
    [InlineData("//evil.example/Reports")]
    [InlineData("/\\evil.example")]
    [InlineData("/%2F%2Fevil.example")]
    [InlineData("/%5Cevil")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData("/Reports?x=1\nSet-Cookie")]
    public void Rejects_off_site_targets(string candidate)
    {
        ReturnUrlRules.Sanitize(candidate).Should().BeNull();
    }

    [Fact]
    public void Trims_a_local_path()
    {
        ReturnUrlRules.Sanitize(" /Reports?status=Submitted ").Should().Be("/Reports?status=Submitted");
    }

    [Fact]
    public void Report_filters_round_trip_and_name_the_list()
    {
        var query = new ReportsListQuery
        {
            Status = ["Submitted"],
            SortBy = "CreateDate",
            SortDir = "desc",
            Page = 2,
            PageSize = 50,
            FacilityId = "abc"
        };
        var path = ToPath("/Reports", query.ToRoute());
        var safe = ReturnUrlRules.Sanitize(path);

        safe.Should().Be(path);
        safe.Should().Contain("status=Submitted");
        safe.Should().Contain("page=2");
        safe.Should().Contain("sortBy=CreateDate");
        safe.Should().Contain("facilityId=abc");
        ReturnUrlRules.Label(safe).Should().Be("Back to reports");

        var detail = ReturnUrlRules.WithReturn("/Tenants/Report/facility", safe);
        detail.Should().StartWith("/Tenants/Report/facility?returnUrl=");
        ReturnUrlRules.Sanitize(Uri.UnescapeDataString(detail.Split("returnUrl=", 2)[1])).Should().Be(path);
    }

    [Fact]
    public void Log_audit_and_facility_filters_round_trip()
    {
        var logs = new AcquisitionQuery
        {
            FacilityId = "fac",
            SortBy = "CreateDate",
            SortDir = "asc",
            Page = 3
        };
        var logPath = ToPath("/Logs/Acquisition", logs.ToRoute());
        ReturnUrlRules.Sanitize(logPath).Should().Be(logPath);
        ReturnUrlRules.Label(logPath).Should().Be("Back to logs");

        var audit = new AuditQuery { FacilityId = "fac", SortBy = "CreatedOn", Page = 2 };
        var auditPath = ToPath("/Logs/Audit", audit.ToRoute());
        ReturnUrlRules.Sanitize(auditPath).Should().Be(auditPath);
        ReturnUrlRules.Label(auditPath).Should().Be("Back to logs");

        var facility = new FacilityViewQuery { Section = "reports", ReportId = "rpt", Page = 4 };
        var facilityPath = ToPath("/Tenants/View/fac", facility.ToRoute());
        ReturnUrlRules.Sanitize(facilityPath).Should().Be(facilityPath);
        ReturnUrlRules.Label(facilityPath).Should().Be("Back to facility");
    }

    [Theory]
    [InlineData("/", "Back to dashboard")]
    [InlineData("/Home", "Back to dashboard")]
    [InlineData("/Tenants", "Back to tenants")]
    [InlineData("/Tenants/Report/abc?reportId=def", "Back to report")]
    [InlineData("/Automation", "Back to runs")]
    [InlineData("/Automation/Run/abc", "Back to run")]
    [InlineData("/Configuration/Notifications?search=a", "Back to notifications")]
    [InlineData("/System/Users?search=a", "Back to users")]
    [InlineData("/Metrics/Scenario/abc", "Back to history")]
    public void Label_matches_the_origin(string url, string label)
    {
        ReturnUrlRules.Label(url).Should().Be(label);
    }

    [Fact]
    public void Tab_nav_keeps_returnUrl_and_each_list_link_carries_the_current_search()
    {
        var nav = File.ReadAllText(Repo("Views/Shared/_ReportNav.cshtml"));
        nav.Should().Contain("ReturnUrlRules.FromQuery");
        nav.Should().Contain("asp-route-returnUrl=\"@origin\"");

        foreach (var relative in new[]
        {
            "Views/Reports/Index.cshtml",
            "Views/Tenants/Index.cshtml",
            "Views/Tenants/_ViewReports.cshtml",
            "Views/Logs/_AcquisitionLogList.cshtml",
            "Views/Logs/Audit.cshtml",
            "Views/Logs/Sftp.cshtml",
            "Views/Automation/Index.cshtml",
            "Views/Automation/_RecentRuns.cshtml",
            "Views/Home/_Overview.cshtml",
            "Views/Configuration/Notifications.cshtml",
            "Views/Configuration/Measures.cshtml",
            "Views/Configuration/Validation.cshtml",
            "Views/Configuration/Vendors.cshtml",
            "Views/System/Users.cshtml",
            "Views/Metrics/Index.cshtml",
            "Views/Metrics/Scenario.cshtml"
        })
        {
            File.ReadAllText(Repo(relative)).Should().Contain("ReturnUrlRules.Here", because: relative);
        }

        foreach (var relative in new[]
        {
            "Views/Reports/Validation.cshtml",
            "Views/Reports/Acquisition.cshtml",
            "Views/Tenants/Report.cshtml",
            "Views/Logs/AcquisitionDetail.cshtml",
            "Views/Logs/AuditDetail.cshtml",
            "Views/Logs/SftpDetail.cshtml",
            "Views/Automation/Run.cshtml"
        })
        {
            File.ReadAllText(Repo(relative)).Should().Contain("_BackButton", because: relative);
        }
    }

    private static string ToPath(string path, Dictionary<string, string> route) =>
        path + "?" + string.Join("&", route.Select(pair =>
            Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value)));

    private static string Repo(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, "DotNet", "Link.UI", relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
