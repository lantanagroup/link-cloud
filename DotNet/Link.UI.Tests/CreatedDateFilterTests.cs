using FluentAssertions;
using Link.UI.Models;
using Link.UI.Services;
using MongoDB.Bson;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// Recent Runs can be narrowed by Created Date. Days are UTC, the end day is inclusive, and the
/// filter rides along with sort, paging and return links. Stats and active runs are not filtered.
/// </summary>
public class CreatedDateFilterTests
{
    [Fact]
    public void Range_uses_utc_midnight_and_an_inclusive_end_day()
    {
        AutomationRules.TryCreatedRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), out var from, out var before, out var error)
            .Should().BeTrue();
        error.Should().BeNull();
        from.Should().Be(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        before.Should().Be(new DateTimeOffset(2026, 10, 16, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Either_end_can_be_left_open()
    {
        AutomationRules.TryCreatedRange(new DateOnly(2026, 10, 1), null, out var from, out var before, out _).Should().BeTrue();
        from.Should().NotBeNull();
        before.Should().BeNull();

        AutomationRules.TryCreatedRange(null, new DateOnly(2026, 10, 1), out from, out before, out _).Should().BeTrue();
        from.Should().BeNull();
        before.Should().Be(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));

        AutomationRules.TryCreatedRange(null, null, out from, out before, out _).Should().BeTrue();
        from.Should().BeNull();
        before.Should().BeNull();
    }

    [Fact]
    public void Same_day_range_covers_that_whole_day()
    {
        var day = new DateOnly(2026, 10, 27);
        AutomationRules.TryCreatedRange(day, day, out var from, out var before, out _).Should().BeTrue();
        (before!.Value - from!.Value).Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public void Reversed_or_out_of_range_dates_are_rejected_like_automation_ui()
    {
        AutomationRules.TryCreatedRange(new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 1), out var from, out var before, out var error)
            .Should().BeFalse();
        error.Should().Be("Created From must be on or before Created To.");
        from.Should().BeNull();
        before.Should().BeNull();

        AutomationRules.TryCreatedRange(null, DateOnly.MaxValue, out _, out _, out error).Should().BeFalse();
        error.Should().Be("Created To is outside the supported date range.");
    }

    [Fact]
    public void Recent_filter_is_a_gte_lt_range_on_created_at()
    {
        var filter = AutomationRunReader.RenderRecentFilter(new AutomationRunQuery
        {
            CreatedFrom = new DateOnly(2026, 10, 1),
            CreatedTo = new DateOnly(2026, 10, 15)
        });

        var created = filter["CreatedAt"].AsBsonDocument;
        created["$gte"].ToUniversalTime().Should().Be(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        created["$lt"].ToUniversalTime().Should().Be(new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void No_or_rejected_dates_leave_recent_runs_unfiltered()
    {
        AutomationRunReader.RenderRecentFilter(new AutomationRunQuery()).Should().BeEquivalentTo(new BsonDocument());
        AutomationRunReader.RenderRecentFilter(new AutomationRunQuery
        {
            CreatedFrom = new DateOnly(2026, 10, 15),
            CreatedTo = new DateOnly(2026, 10, 1)
        }).Should().BeEquivalentTo(new BsonDocument());
    }

    [Fact]
    public void Count_and_rows_share_the_filter_but_stats_and_active_runs_do_not()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/AutomationRunReader.cs"));
        var start = text.IndexOf("public async Task<AutomationDashboardPage> LoadDashboardAsync", StringComparison.Ordinal);
        var end = text.IndexOf("internal static BsonDocument RenderRecentFilter", StringComparison.Ordinal);
        var body = text[start..end];

        body.Should().MatchRegex(@"CountDocumentsAsync\(\s*recentFilter,");
        body.Should().MatchRegex(@"FindRowsAsync\(\s*recentFilter,\s*Sort\(sortBy, descending\)");
        body.Should().MatchRegex(@"windowRows = await FindRowsAsync\(\s*Builders<AutomationRunDocument>\.Filter\.Gte\(row => row\.CreatedAt, since\)");
        body.Should().Contain("var active = await LoadActiveAsync(token);");
        body.Should().Contain("CreatedFrom = created.From");
        body.Should().Contain("CreatedTo = created.To");
    }

    [Fact]
    public void Recent_runs_card_carries_the_dates_through_sort_pager_and_inputs()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Automation/_RecentRuns.cshtml"));

        text.Should().Contain("sortBy = column, sortDir = SortDirFor(column), createdFrom, createdTo");
        text.Should().Contain("sortBy = Model.SortBy, sortDir = Model.SortDir, createdFrom, createdTo");
        text.Should().Contain("data-created-from=\"@createdFrom\"");
        text.Should().Contain("data-created-to=\"@createdTo\"");
        text.Should().Contain("<label for=\"createdFromInput\" class=\"form-label small mb-1\">Created from (UTC)</label>");
        text.Should().Contain("<label for=\"createdToInput\" class=\"form-label small mb-1\">Created to (UTC)</label>");
        text.Should().Contain("id=\"clearCreatedDateFilter\"");
        text.Should().Contain("No runs found for the selected Created Date range.");
        text.Should().Contain("data-created-error");
    }

    [Fact]
    public void Dashboard_script_sends_and_keeps_the_dates()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/automation-dashboard.js"));

        text.Should().Contain("params.set(\"createdFrom\", createdFrom)");
        text.Should().Contain("params.set(\"createdTo\", createdTo)");
        text.Should().Contain("[\"pageNumber\", \"pageSize\", \"sortBy\", \"sortDir\", \"createdFrom\", \"createdTo\"]");
        text.Should().Contain("closest(\"#recentRunsDateFilter\")");
        text.Should().Contain("closest(\"#clearCreatedDateFilter\")");
        text.Should().Contain("Created From must be on or before Created To.");
        text.Should().Contain("keepTypedCreatedDates(host, typed)");
    }

    [Fact]
    public void Return_links_from_the_partial_keep_the_dates()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Path = "/Automation/recent";
        context.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?pageNumber=2&createdFrom=2026-10-01&createdTo=2026-10-15");

        ReturnUrlRules.Here(context.Request)
            .Should().Be("/Automation?pageNumber=2&createdFrom=2026-10-01&createdTo=2026-10-15");
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
