using FluentAssertions;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DMRP;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Link.UI.Models;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class FacilityViewRulesTests
{
    [Theory]
    [InlineData(null, "reports")]
    [InlineData("  ", "reports")]
    [InlineData("HSLOC", "hsloc")]
    [InlineData("dashboard", "reports")]
    public void Unknown_section_falls_back_to_reports(string? section, string expected)
    {
        FacilityViewRules.NormalizeSection(section).Should().Be(expected);
    }

    [Fact]
    public void Page_size_outside_the_allowed_set_uses_the_default()
    {
        FacilityViewRules.ClampPage(0).Should().Be(1);
        FacilityViewRules.ClampPageSize(7).Should().Be(FacilityViewRules.DefaultPageSize);
        FacilityViewRules.ClampPageSize(20).Should().Be(20);
    }

    [Fact]
    public void Period_end_includes_the_chosen_calendar_day()
    {
        var day = new DateOnly(2026, 3, 15);
        FacilityViewRules.PeriodStart(day).Should().Be(new DateTime(2026, 3, 15, 0, 0, 0));
        FacilityViewRules.PeriodEnd(day).Should().Be(new DateTime(2026, 3, 15, 23, 59, 59));
        FacilityViewRules.PeriodEnd(null).Should().BeNull();
    }

    [Fact]
    public void When_shows_labeled_utc_for_both_clocks()
    {
        var instant = new DateTime(2026, 10, 7, 12, 49, 40, DateTimeKind.Utc);

        FacilityViewRules.When(instant).Should().Be("2026-10-07 12:49:40 UTC");
        FacilityViewRules.When((DateTime?)instant).Should().Be(LinkUiTime.Display(instant));
        FacilityViewRules.When(default(DateTime)).Should().BeEmpty();
        FacilityViewRules.When((DateTime?)null).Should().BeEmpty();
    }

    [Fact]
    public void Blank_report_id_is_no_filter_and_a_non_guid_is_an_error()
    {
        FacilityViewRules.ParseReportId("  ", out var blank).Should().BeNull();
        blank.Should().BeNull();

        FacilityViewRules.ParseReportId("not-a-guid", out var error).Should().BeNull();
        error.Should().Be("Report ID is not a valid id.");

        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        FacilityViewRules.ParseReportId(id.ToString(), out var none).Should().Be(id);
        none.Should().BeNull();
    }

    [Fact]
    public void Status_filter_accepts_a_comma_list_and_ignores_unknown_names()
    {
        var parsed = FacilityViewRules.ParseStatuses(["New, Submitted", "nope", "New"]);
        parsed.Should().Equal(ScheduleStatus.New, ScheduleStatus.Submitted);
    }

    [Theory]
    [InlineData(ScheduleStatus.EndOfPeriod, "End of Period")]
    [InlineData(ScheduleStatus.CompletedNotSubmitted, "Completed (Submission Skipped)")]
    [InlineData(ScheduleStatus.Submitted, "Submitted")]
    public void Status_labels_match_the_facility_view(ScheduleStatus status, string label)
    {
        FacilityViewRules.StatusLabel(status).Should().Be(label);
    }

    [Fact]
    public void Report_actions_follow_status_and_deleted()
    {
        FacilityViewRules.CanResubmit(ScheduleStatus.Submitted, deleted: false).Should().BeTrue();
        FacilityViewRules.CanResubmit(ScheduleStatus.CompletedNotSubmitted, deleted: false).Should().BeTrue();
        FacilityViewRules.CanResubmit(ScheduleStatus.Submitted, deleted: true).Should().BeFalse();
        FacilityViewRules.CanResubmit(ScheduleStatus.New, deleted: false).Should().BeFalse();
        FacilityViewRules.CanResubmit(ScheduleStatus.Submitted, deleted: false, isTest: true).Should().BeFalse();
        FacilityViewRules.CanResubmit(ScheduleStatus.CompletedNotSubmitted, deleted: false, isTest: true).Should().BeFalse();
        FacilityViewRules.CanResubmit(ScheduleStatus.Submitted, deleted: false, isTest: false).Should().BeTrue();

        FacilityViewRules.CanAbort(ScheduleStatus.New, deleted: false).Should().BeTrue();
        FacilityViewRules.CanAbort(ScheduleStatus.EndOfPeriod, deleted: false).Should().BeTrue();
        FacilityViewRules.CanAbort(ScheduleStatus.Submitted, deleted: false).Should().BeFalse();
        FacilityViewRules.CanAbort(ScheduleStatus.New, deleted: true).Should().BeFalse();

        FacilityViewRules.CanCleanUp(ScheduleStatus.Scheduled, deleted: false).Should().BeTrue();
        FacilityViewRules.CanCleanUp(ScheduleStatus.New, deleted: false).Should().BeFalse();
        FacilityViewRules.CanCleanUp(ScheduleStatus.Submitted, deleted: true).Should().BeFalse();

        FacilityViewRules.CanRestore(deleted: true).Should().BeTrue();
        FacilityViewRules.CanRestore(deleted: false).Should().BeFalse();
    }

    [Fact]
    public void Enrolled_reporting_treats_null_arrays_as_empty()
    {
        var enrolled = FacilityViewRules.Enrolled(new TenantScheduledReportConfig
        {
            Daily = ["NHSN"],
            Weekly = null!,
            Monthly = null!
        });

        enrolled.Select(item => item.Cadence).Should().Equal("Daily", "Weekly", "Monthly");
        enrolled[0].Measures.Should().Equal("NHSN");
        enrolled[1].Measures.Should().BeEmpty();
        enrolled[2].Measures.Should().BeEmpty();
        FacilityViewRules.Enrolled(null)[0].Measures.Should().BeEmpty();
    }

    [Fact]
    public void Location_tree_nests_children_and_marks_mapped_locations()
    {
        var hsloc = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var roots = FacilityViewRules.BuildLocationTree(
        [
            Location("icu", "ward", "ICU", hsloc),
            Location("ward", null, "Ward", null),
            Location("icu", "other", "Duplicate", hsloc)
        ]);

        roots.Should().ContainSingle();
        roots[0].LocationId.Should().Be("ward");
        roots[0].Mapped.Should().BeFalse();
        roots[0].Children.Should().ContainSingle();
        roots[0].Children[0].LocationId.Should().Be("icu");
        roots[0].Children[0].Mapped.Should().BeTrue();
        roots[0].Children[0].Mappings.Should().ContainSingle().Which.Hsloc.Should().NotBe("Unmapped");
    }

    [Fact]
    public void Location_tree_breaks_a_cycle_and_keeps_an_unknown_parent_as_a_root()
    {
        var roots = FacilityViewRules.BuildLocationTree(
        [
            Location("a", "b", "A", null),
            Location("b", "a", "B", null),
            Location("orphan", "missing", "Orphan", null)
        ]);

        roots.Select(node => node.LocationId).Should().BeEquivalentTo("a", "orphan");
        var a = roots.Single(node => node.LocationId == "a");
        a.Children.Should().ContainSingle().Which.LocationId.Should().Be("b");
        a.Children[0].Children.Should().BeEmpty();
        FacilityViewRules.FindLocation(roots, "b")!.Name.Should().Be("B");
        FacilityViewRules.FindLocation(roots, "missing").Should().BeNull();
    }

    [Fact]
    public void Plans_default_to_the_newest_period_and_honor_the_filters()
    {
        var plans = new[]
        {
            Plan("CAUTI", "dqm-a", Frequency.Monthly, 2025, 1, reporting: true),
            Plan("CLABSI", "dqm-b", Frequency.Weekly, 2026, 3, reporting: false),
            Plan("CAUTI", "dqm-a", Frequency.Monthly, 2026, 3, reporting: true)
        };

        var visible = FacilityViewRules.VisiblePlans(plans, new FacilityViewQuery(), out var paging);
        visible.Select(row => row.Measure + " " + row.Period).Should().Equal(
            "CAUTI March 2026",
            "CLABSI March 2026",
            "CAUTI January 2025");
        paging.TotalCount.Should().Be(3);

        var filtered = FacilityViewRules.VisiblePlans(plans, new FacilityViewQuery
        {
            Measure = "cauti",
            Period = "2026-3",
            Reporting = "true",
            Cadence = "monthly",
            PageSize = 5,
            Page = 9
        }, out var filteredPaging);

        filtered.Should().ContainSingle().Which.Measure.Should().Be("CAUTI");
        filteredPaging.Page.Should().Be(1);
        filteredPaging.PageSize.Should().Be(5);

        FacilityViewRules.PeriodOptions(plans).Select(option => option.Key).Should().Equal("2026-3", "2025-1");
        FacilityViewRules.CadenceOptions(plans).Should().Equal("Weekly", "Monthly");
    }

    private static FacilityLocationTreeApiModel Location(string id, string? partOf, string name, Guid? hsloc) =>
        new()
        {
            Id = id,
            LocationId = id,
            PartOfId = partOf,
            LocationName = name,
            Mappings =
            [
                new FacilityLocationTreeMappingApiModel
                {
                    Id = id + "-map",
                    LocalCodeSystem = "local",
                    LocalCode = id,
                    HSLOCId = hsloc,
                    HSLOCCode = hsloc is null ? null : "1025-2"
                }
            ]
        };

    private static FacilityReportingPlanModel Plan(
        string measure,
        string dqm,
        Frequency frequency,
        int year,
        int month,
        bool reporting) =>
        new()
        {
            Measure = measure,
            DQM = dqm,
            Frequency = frequency,
            ReportingYear = year,
            ReportingMonth = month,
            IsReporting = reporting
        };
}
