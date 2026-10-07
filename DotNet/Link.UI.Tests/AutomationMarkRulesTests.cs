using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class AutomationMarkRulesTests
{
    private static readonly DateTimeOffset Older = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newer = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_run_id_a_created_facility_and_a_tombstone_are_owned()
    {
        var runId = Guid.Parse("afb7185a-b94a-4580-8e89-c4e6724073aa");
        var created = Guid.Parse("f172a29b-71c6-4a8b-8bac-857440411867");
        var index = AutomationMarkRules.Build(
            [
                new AutomationRunMark(runId, "other-tenant", false, Older),
                new AutomationRunMark(created, "nhsn-org", true, Newer)
            ],
            [new AutomationTombstoneMark("gone-facility", "61c1f64e-b9ce-40f5-88eb-d1fa3fcfac05", Older)]);

        index.RunIdFor(runId.ToString()).Should().Be(runId.ToString("D"));
        index.RunIdFor("NHSN-ORG").Should().Be(created.ToString("D"));
        index.RunIdFor("gone-facility").Should().Be("61c1f64e-b9ce-40f5-88eb-d1fa3fcfac05");
        index.Contains("other-tenant").Should().BeFalse();
    }

    [Fact]
    public void A_live_created_facility_keeps_its_run_over_a_tombstone_and_an_older_run()
    {
        var older = Guid.Parse("595c76d6-77a2-4a88-ade6-b4c4708f540a");
        var newer = Guid.Parse("e2d21451-2381-4df7-b15c-bb7e2a6b9c48");
        var index = AutomationMarkRules.Build(
            [
                new AutomationRunMark(older, "shared", true, Older),
                new AutomationRunMark(newer, "shared", true, Newer)
            ],
            [new AutomationTombstoneMark("shared", "afb7185a-b94a-4580-8e89-c4e6724073aa", Newer)]);

        index.RunIdFor("shared").Should().Be(newer.ToString("D"));
    }

    [Fact]
    public void An_unowned_guid_is_not_marked_and_the_filter_defaults_to_all()
    {
        var index = AutomationMarkRules.Build([], []);
        index.Contains(Guid.NewGuid().ToString()).Should().BeFalse();
        AutomationMarkRules.NormalizeScope(null).Should().Be("all");
        AutomationMarkRules.NormalizeScope(" AUTOMATION ").Should().Be("automation");
        AutomationMarkRules.NormalizeScope("<script>").Should().Be("all");
    }

    [Fact]
    public void The_facility_search_keeps_the_newest_forty()
    {
        var runs = Enumerable.Range(0, 41).Select(i => new AutomationRunMark(
            Guid.NewGuid(),
            null,
            false,
            Older.AddMinutes(i))).ToList();

        var index = AutomationMarkRules.Build(runs, []);
        var ids = index.NewestFacilityIds(AutomationMarkRules.MaxFacilitySearches, out var truncated);

        truncated.Should().BeTrue();
        ids.Should().HaveCount(40);
        ids[0].Should().Be(runs[^1].RunId.ToString("D"));
    }

    [Fact]
    public void The_combined_note_says_when_the_list_is_capped_or_only_the_first_page()
    {
        AutomationMarkRules.SearchNote(false, false).Should().BeNull();
        AutomationMarkRules.SearchNote(true, false).Should().Contain("40");
        AutomationMarkRules.SearchNote(false, true).Should().Contain("first page");
        AutomationMarkRules.SearchNote(true, true).Should().Contain("40").And.Contain("first page");
    }

    [Fact]
    public void Paging_the_merged_rows_uses_the_caller_page_size()
    {
        var items = Enumerable.Range(1, 25).ToList();

        var page = AutomationMarkRules.Slice(items, 2, 10);

        page.Page.Should().Be(2);
        page.Size.Should().Be(10);
        page.Total.Should().Be(25);
        page.Pages.Should().Be(3);
        page.Items.Should().Equal(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
    }

    [Theory]
    [InlineData("DotNet/Link.UI/Views/Tenants/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Reports/Index.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Logs/Acquisition.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Logs/Sftp.cshtml")]
    [InlineData("DotNet/Link.UI/Views/Logs/Audit.cshtml")]
    public void The_list_pages_badge_rows_and_keep_refreshing(string relativePath)
    {
        var view = File.ReadAllText(RepoFile(relativePath));

        view.Should().Contain("name=\"_AutomationScope\"");
        view.Should().Contain("name=\"_AutomationBadge\"");
        view.Should().Contain("data-au-refresh=");
        view.Should().Contain("data-au-filter=");
    }

    [Fact]
    public void Development_expects_the_organization_resource_the_report_service_writes()
    {
        var development = File.ReadAllText(RepoFile("DotNet/Link.UI/appsettings.Development.json"));
        development.Should().Contain("\"IncludeOrganizationResource\": true");

        var executor = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/RunExecutor.cs"));
        executor.Should().Contain("return (false, \"default(false)\")");
        Directory.GetFiles(RepoFile("DotNet/Link.UI"), "appsettings*.json")
            .Select(Path.GetFileName)
            .Should().NotContain("appsettings.Docker.json");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
