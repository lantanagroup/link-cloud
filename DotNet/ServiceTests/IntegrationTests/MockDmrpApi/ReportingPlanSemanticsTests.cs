using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Mapping;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.MockDmrpApi;

/// <summary>
/// Covers the rule the whole service exists to express: enrollment is conveyed by presence.
/// </summary>
/// <remarks>
/// A measure missing from a plan means the facility is not enrolled in it. There is no
/// negative representation, so every way a measure could wrongly appear or wrongly vanish
/// is a way to mislead a consumer about what a facility is reporting.
/// <para>
/// Against a real database rather than the in-memory fake, because the isolation properties
/// here are enforced partly by the query and partly by the schema -- a nullable month in a
/// unique index behaves differently in SQL Server than in LINQ-to-objects, and it is the SQL
/// Server behaviour that ships.
/// </para>
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "IntegrationTests")]
public class ReportingPlanSemanticsTests
{
    private readonly MockDmrpApiIntegrationTestFixture _fixture;

    public ReportingPlanSemanticsTests(MockDmrpApiIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>A monthly (MSC) entry.</summary>
    private static ReportingPlanEntryEntity Entry(
        string facilityId = "F1", string measure = "HOB", int month = 5, int year = 2026, string isReporting = "Y") =>
        new()
        {
            FacilityId = facilityId,
            Component = ReportingComponents.Msc,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting
        };

    /// <summary>A patient-safety (PS) entry. Reported monthly, like every other component.</summary>
    private static ReportingPlanEntryEntity PatientSafetyEntry(
        string facilityId = "F1", string measure = "HAI", int month = 5, int year = 2026,
        string isReporting = "Y") =>
        new()
        {
            FacilityId = facilityId,
            Component = ReportingComponents.Ps,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting
        };

    private async Task SeedAsync(params ReportingPlanEntryEntity[] entries)
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingPlanService>();

        foreach (var entry in entries)
        {
            await service.CreateAsync(entry, CancellationToken.None);
        }
    }

    private async Task<IReadOnlyList<ReportingPlanEntryEntity>> MonthlyPlanAsync(
        string facilityId = "F1", int month = 5, int year = 2026)
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingPlanService>();

        return await service.GetReportingPlanAsync(
            ReportingComponents.Msc, facilityId, null, month, year, CancellationToken.None);
    }

    private async Task<IReadOnlyList<ReportingPlanEntryEntity>> PatientSafetyPlanAsync(
        string facilityId = "F1", int? month = null, int year = 2026)
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingPlanService>();

        return await service.GetReportingPlanAsync(
            ReportingComponents.Ps, facilityId, null, month, year, CancellationToken.None);
    }

    // ------------------------------------------------------ presence semantics

    [Fact]
    public async Task APlanListsOnlyTheMeasuresTheFacilityIsEnrolledIn()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(measure: "HOB"));

        var plan = await MonthlyPlanAsync();

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HOB");

        // The absence of HTCDI is the entire signal that the facility does not report it.
        plan.Should().NotContain(e => e.Measure == "HTCDI");
    }

    [Fact]
    public async Task AFacilityEnrolledInNothingProducesAnEmptyPlanRatherThanAnError()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(facilityId: "SomebodyElse"));

        var plan = await MonthlyPlanAsync();

        plan.Should().BeEmpty();

        // And the projection turns that into an empty array, not a null a consumer would
        // dereference on the most ordinary case there is.
        var response = EntryMapper.ToReportingPlan("F1", 5, 2026, plan, DateTimeOffset.UtcNow);
        response.Plans.Should().NotBeNull();
        response.Plans.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEntryMarkedAsNotReportingIsExcluded()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(measure: "HOB"), Entry(measure: "HTCDI", isReporting: "N"));

        var plan = await MonthlyPlanAsync();

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HOB");
    }

    // ------------------------------------------------------------- isolation

    [Fact]
    public async Task PeriodsAreIsolatedFromOneAnother()
    {
        await _fixture.ResetAsync();
        await SeedAsync(
            Entry(measure: "HOB", month: 6),
            Entry(measure: "HTCDI", year: 2025));

        (await MonthlyPlanAsync(month: 5, year: 2026)).Should().BeEmpty();
        (await MonthlyPlanAsync(month: 6, year: 2026)).Should().ContainSingle();
        (await MonthlyPlanAsync(month: 5, year: 2025)).Should().ContainSingle();
    }

    [Fact]
    public async Task FacilitiesAreIsolatedFromOneAnother()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(facilityId: "F1"), Entry(facilityId: "F2", measure: "HTCDI"));

        var first = await MonthlyPlanAsync("F1");
        var second = await MonthlyPlanAsync("F2");

        first.Should().ContainSingle();
        first[0].Measure.Should().Be("HOB");
        second.Should().ContainSingle();
        second[0].Measure.Should().Be("HTCDI");
    }

    [Fact]
    public async Task ComponentsAreIsolatedFromOneAnother()
    {
        // Both plans come out of one table, and the annual query does not filter on month.
        // Without the component in the predicate that omission would pull every monthly
        // entry for the year into the annual plan, and the response would still look
        // perfectly well formed.
        await _fixture.ResetAsync();
        await SeedAsync(
            Entry(measure: "HOB"),
            Entry(measure: "HTCDI", month: 6),
            PatientSafetyEntry(measure: "HAI"),
            PatientSafetyEntry(measure: "SSI"));

        var monthly = await MonthlyPlanAsync(month: 5, year: 2026);
        var annual = await PatientSafetyPlanAsync(year: 2026);

        monthly.Select(e => e.Measure).Should().BeEquivalentTo("HOB");
        annual.Select(e => e.Measure).Should().BeEquivalentTo("HAI", "SSI");
    }

    [Fact]
    public async Task TheSameMeasureNameCanAppearInBothComponents()
    {
        // The two components are independent plans. A shared measure name is a legitimate
        // pair of rows, and the unique index must not treat it as a duplicate.
        await _fixture.ResetAsync();
        await SeedAsync(Entry(measure: "SHARED"), PatientSafetyEntry(measure: "SHARED"));

        (await MonthlyPlanAsync()).Should().ContainSingle();
        (await PatientSafetyPlanAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task APatientSafetyPlanNarrowsByMonthLikeTheMedicinePlan()
    {
        // Patient safety is reported monthly, so the month is a real predicate here and not
        // a parameter that gets dropped. Seeding a second month proves it narrows rather
        // than returning the whole year.
        await _fixture.ResetAsync();
        await SeedAsync(
            PatientSafetyEntry(measure: "HAI", month: 5),
            PatientSafetyEntry(measure: "SSI", month: 5),
            PatientSafetyEntry(measure: "CLABSI", month: 6));

        var plan = await PatientSafetyPlanAsync(month: 5, year: 2026);

        plan.Should().HaveCount(2);
        plan.Should().OnlyContain(e => e.ReportingMonth == 5);
    }

    [Fact]
    public async Task AnnualYearsAreIsolatedFromOneAnother()
    {
        await _fixture.ResetAsync();
        await SeedAsync(PatientSafetyEntry(measure: "HAI"), PatientSafetyEntry(measure: "SSI", year: 2025));

        (await PatientSafetyPlanAsync(year: 2026)).Should().ContainSingle();
        (await PatientSafetyPlanAsync(year: 2025)).Should().ContainSingle();
        (await PatientSafetyPlanAsync(year: 2024)).Should().BeEmpty();
    }
}
