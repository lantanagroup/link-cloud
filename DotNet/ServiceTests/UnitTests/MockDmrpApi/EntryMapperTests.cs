using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Mapping;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using Xunit;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// Covers the projection into the third-party contract type. The support surface has its own
/// mapper and its own tests; see <see cref="MockEntryMapperTests"/>.
/// </summary>
public class EntryMapperTests
{
    private static ReportingPlanEntryEntity Entry(
        string measure = "HOB",
        string isReporting = "Y",
        int? month = 5,
        string component = ReportingComponents.Msc) => new()
        {
            Id = "11111111-1111-1111-1111-111111111111",
            FacilityId = "F1",
            Component = component,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = 2026,
            IsReporting = isReporting,
            CreateDate = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            ModifyDate = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc)
        };

    [Fact]
    public void ToReportingPlan_ListsOnlyTheSuppliedMeasures()
    {
        var retrievedOn = DateTimeOffset.UtcNow;

        var plan = EntryMapper.ToReportingPlan("F1", 5, 2026, [Entry(measure: "HOB")], retrievedOn);

        plan.Orgid.Should().BeNull("F1 is not numeric, so it cannot be represented by the root orgid");
        plan.Month.Should().Be(5);
        plan.Year.Should().Be(2026);
        plan.CreateDate.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{2}$",
            "the real API's format is a space separator with no timezone, not ISO 8601");
        plan.Plans.Should().ContainSingle();
        plan.Plans.Single().Name.Should().Be("HOB");
        plan.Plans.Single().Reporting.Should().Be("Y");

        // The absence of HTCDI is what tells a caller the facility is not enrolled in it.
        plan.Plans.Should().NotContain(m => m.Name == "HTCDI");
    }

    [Fact]
    public void ToReportingPlan_ForAnAnnualPlan_OmitsTheReportingMonth()
    {
        // /ps/annual/mrp has no month to report. Emitting a zero or a stale value would tell a
        // consumer the plan covers one particular month, which is the opposite of the truth.
        var plan = EntryMapper.ToReportingPlan(
            "F1", null, 2026,
            [Entry(measure: "HAI", month: null, component: ReportingComponents.Ps)],
            DateTimeOffset.UtcNow);

        plan.Month.Should().BeNull();
        plan.Year.Should().Be(2026);
        plan.Plans.Should().ContainSingle();
    }

    [Fact]
    public void ToReportingPlan_WithNoEntries_ProducesAnEmptyMeasuresArrayNotNull()
    {
        // A facility enrolled in nothing is a meaningful answer, and the caller iterates
        // measures unconditionally. A null here would be a NullReferenceException in
        // consumer code for the most ordinary case there is.
        var plan = EntryMapper.ToReportingPlan("F1", 5, 2026, [], DateTimeOffset.UtcNow);

        plan.Plans.Should().NotBeNull();
        plan.Plans.Should().BeEmpty();
    }

    [Fact]
    public void ToReportingPlan_RejectsNullEntries()
    {
        ((Action)(() => EntryMapper.ToReportingPlan("F1", 5, 2026, null!, DateTimeOffset.UtcNow)))
            .Should().Throw<ArgumentNullException>();
    }
}
