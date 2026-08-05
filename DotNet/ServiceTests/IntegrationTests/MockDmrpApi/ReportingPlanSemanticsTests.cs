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

    private static ReportingPlanEntryEntity Entry(
        string facilityId = "F1", string measure = "HOB", int month = 5, int year = 2026, string isReporting = "Y") =>
        new()
        {
            FacilityId = facilityId,
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

    private async Task<IReadOnlyList<ReportingPlanEntryEntity>> PlanAsync(
        string facilityId = "F1", int month = 5, int year = 2026)
    {
        using var scope = _fixture.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReportingPlanService>();

        return await service.GetReportingPlanAsync(facilityId, month, year, CancellationToken.None);
    }

    [Fact]
    public async Task APlanListsOnlyTheMeasuresTheFacilityIsEnrolledIn()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(measure: "HOB"));

        var plan = await PlanAsync();

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

        var plan = await PlanAsync();

        plan.Should().BeEmpty();

        // And the projection turns that into an empty array, not a null a consumer would
        // dereference on the most ordinary case there is.
        var response = EntryMapper.ToReportingPlan("F1", 5, 2026, plan, DateTimeOffset.UtcNow);
        response.Measures.Should().NotBeNull();
        response.Measures.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEntryMarkedAsNotReportingIsExcluded()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(measure: "HOB"), Entry(measure: "HTCDI", isReporting: "N"));

        var plan = await PlanAsync();

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task PeriodsAreIsolatedFromOneAnother()
    {
        await _fixture.ResetAsync();
        await SeedAsync(
            Entry(measure: "HOB", month: 6),
            Entry(measure: "HTCDI", year: 2025));

        (await PlanAsync(month: 5, year: 2026)).Should().BeEmpty();
        (await PlanAsync(month: 6, year: 2026)).Should().ContainSingle();
        (await PlanAsync(month: 5, year: 2025)).Should().ContainSingle();
    }

    [Fact]
    public async Task FacilitiesAreIsolatedFromOneAnother()
    {
        await _fixture.ResetAsync();
        await SeedAsync(Entry(facilityId: "F1"), Entry(facilityId: "F2", measure: "HTCDI"));

        var first = await PlanAsync("F1");
        var second = await PlanAsync("F2");

        first.Should().ContainSingle();
        first[0].Measure.Should().Be("HOB");
        second.Should().ContainSingle();
        second[0].Measure.Should().Be("HTCDI");
    }
}
