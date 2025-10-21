using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Context;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.MockDmrpApi;

/// <summary>
/// Covers behaviour that only appears against a real database: the save interceptor, LINQ
/// that has to translate to SQL, and the unique index.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "IntegrationTests")]
public class ReportingPlanPersistenceTests
{
    private readonly MockDmrpApiIntegrationTestFixture _fixture;

    public ReportingPlanPersistenceTests(MockDmrpApiIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>A monthly (MSC) entry.</summary>
    private static ReportingPlanEntryEntity Entry(
        string facilityId = "F1", string measure = "HOB", int month = 5, int year = 2026) =>
        new()
        {
            FacilityId = facilityId,
            Component = ReportingComponents.Msc,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = "Y"
        };

    /// <summary>A patient-safety (PS) entry, reported monthly exactly as medicine is.</summary>
    private static ReportingPlanEntryEntity PatientSafetyEntry(
        string facilityId = "F1", string measure = "HAI", int month = 5, int year = 2026) =>
        new()
        {
            FacilityId = facilityId,
            Component = ReportingComponents.Ps,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = "Y"
        };

    private async Task<T> WithServiceAsync<T>(Func<IReportingPlanService, Task<T>> action)
    {
        using var scope = _fixture.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IReportingPlanService>());
    }

    [Fact]
    public async Task CreateStampsCreateDateAndUpdateStampsModifyDate()
    {
        // Both come from the shared save interceptor rather than from the service, so this
        // is the only place that wiring is actually proven.
        await _fixture.ResetAsync();

        var created = await WithServiceAsync(s => s.CreateAsync(Entry(), CancellationToken.None));

        created.CreateDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        created.ModifyDate.Should().BeNull("nothing has modified it yet");

        created.IsReporting = "N";
        var updated = await WithServiceAsync(s => s.UpdateAsync(created, CancellationToken.None));

        updated!.ModifyDate.Should().NotBeNull();
        updated.ModifyDate!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task TheUniqueIndexRejectsADuplicateNaturalKey()
    {
        await _fixture.ResetAsync();
        await WithServiceAsync(s => s.CreateAsync(Entry(), CancellationToken.None));

        // The service pre-checks, but the index is the actual guarantee. Writing straight
        // through the context bypasses the pre-check so the constraint itself is tested.
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();

        var duplicate = Entry();
        duplicate.Id = Guid.NewGuid().ToString();
        context.ReportingPlanEntries.Add(duplicate);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public void TheUniqueIndexSpansTheComponentAndThePeriod()
    {
        // Two components can be enrolled in the same measure for the same period, and one
        // component in the same measure across months. Both stay distinct only because the
        // key includes the component and the month.
        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();

        var index = context.Model
            .FindEntityType(typeof(ReportingPlanEntryEntity))!
            .GetIndexes()
            .Single(i => i.IsUnique);

        index.Properties.Select(p => p.Name).Should().Contain(
            [nameof(ReportingPlanEntryEntity.Component), nameof(ReportingPlanEntryEntity.ReportingMonth)]);
    }

    [Fact]
    public async Task TheServiceRejectsADuplicatePatientSafetyEntry()
    {
        // Run against a real provider rather than the in-memory fake: the pre-check is a
        // query, and whether it matches an existing row is the provider's answer to give.
        await _fixture.ResetAsync();
        await WithServiceAsync(s => s.CreateAsync(PatientSafetyEntry(), CancellationToken.None));

        var act = async () => await WithServiceAsync(s => s.CreateAsync(PatientSafetyEntry(), CancellationToken.None));

        await act.Should().ThrowAsync<DuplicateReportingPlanEntryException>();

        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();
        (await context.ReportingPlanEntries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TheUniqueIndexAllowsTheSameMeasureInAnotherPeriodOrFacility()
    {
        await _fixture.ResetAsync();

        await WithServiceAsync(s => s.CreateAsync(Entry(), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(month: 6), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(year: 2027), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(facilityId: "F2"), CancellationToken.None));

        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();

        (await context.ReportingPlanEntries.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task TheUniqueIndexTreatsTheComponentAsPartOfTheKey()
    {
        // The same measure name under both components is two legitimate rows, not a
        // duplicate -- the plans are independent.
        await _fixture.ResetAsync();

        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "SHARED"), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(PatientSafetyEntry(measure: "SHARED"), CancellationToken.None));

        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();

        (await context.ReportingPlanEntries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task APatientSafetyEntryRoundTripsThroughTheDatabaseWithItsMonth()
    {
        // Patient safety is stored exactly like medicine: same columns, same required month.
        await _fixture.ResetAsync();

        var created = await WithServiceAsync(s => s.CreateAsync(PatientSafetyEntry(), CancellationToken.None));

        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();
        var stored = await context.ReportingPlanEntries.SingleAsync(e => e.Id == created.Id);

        stored.ReportingMonth.Should().Be(created.ReportingMonth);
        stored.Component.Should().Be(ReportingComponents.Ps);
    }

    [Fact]
    public async Task SearchTranslatesEveryFilterToSql()
    {
        // The predicate is built as an expression tree and must translate; a construct the
        // provider cannot handle throws here but not against the in-memory fake.
        await _fixture.ResetAsync();
        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "HOB"), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "HTCDI"), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(facilityId: "F2"), CancellationToken.None));

        var criteria = new ReportingPlanSearchCriteria
        {
            FacilityId = "F1",
            Measure = "hob",
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = "Y"
        };

        var (records, metadata) = await WithServiceAsync(s => s.SearchAsync(criteria, CancellationToken.None));

        records.Should().ContainSingle();
        records[0].Measure.Should().Be("HOB", "the measure filter is case-insensitive");
        metadata.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData(ReportingPlanSortBy.FacilityId)]
    [InlineData(ReportingPlanSortBy.Component)]
    [InlineData(ReportingPlanSortBy.Measure)]
    [InlineData(ReportingPlanSortBy.ReportingMonth)]
    [InlineData(ReportingPlanSortBy.ReportingYear)]
    [InlineData(ReportingPlanSortBy.CreateDate)]
    [InlineData(ReportingPlanSortBy.ModifyDate)]
    public async Task EverySortFieldOrdersAgainstTheDatabase(ReportingPlanSortBy sortBy)
    {
        // The shared repository builds the ordering by property name. A name that does not
        // translate fails here rather than as a 500 in front of a user.
        await _fixture.ResetAsync();
        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "HOB"), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "HTCDI"), CancellationToken.None));

        var criteria = new ReportingPlanSearchCriteria { SortBy = sortBy, SortOrder = SortOrder.Ascending };

        var (records, _) = await WithServiceAsync(s => s.SearchAsync(criteria, CancellationToken.None));

        records.Should().HaveCount(2);
    }

    [Fact]
    public async Task PagingReturnsDistinctPagesAndAnAccurateTotal()
    {
        await _fixture.ResetAsync();
        foreach (var measure in new[] { "M1", "M2", "M3", "M4", "M5" })
        {
            await WithServiceAsync(s => s.CreateAsync(Entry(measure: measure), CancellationToken.None));
        }

        var first = await WithServiceAsync(s => s.SearchAsync(
            new ReportingPlanSearchCriteria { PageSize = 2, PageNumber = 1, SortBy = ReportingPlanSortBy.Measure, SortOrder = SortOrder.Ascending },
            CancellationToken.None));

        var second = await WithServiceAsync(s => s.SearchAsync(
            new ReportingPlanSearchCriteria { PageSize = 2, PageNumber = 2, SortBy = ReportingPlanSortBy.Measure, SortOrder = SortOrder.Ascending },
            CancellationToken.None));

        first.Records.Should().HaveCount(2);
        second.Records.Should().HaveCount(2);
        first.Metadata.TotalCount.Should().Be(5);
        first.Metadata.TotalPages.Should().Be(3);
        second.Records.Select(r => r.Measure).Should().NotIntersectWith(first.Records.Select(r => r.Measure));
    }

    [Fact]
    public async Task DeleteByFacilityRemovesOnlyThatFacility()
    {
        await _fixture.ResetAsync();
        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "HOB"), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(measure: "HTCDI"), CancellationToken.None));
        await WithServiceAsync(s => s.CreateAsync(Entry(facilityId: "F2"), CancellationToken.None));

        var removed = await WithServiceAsync(s => s.DeleteByFacilityAsync("F1", CancellationToken.None));

        removed.Should().Be(2);

        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();
        var remaining = await context.ReportingPlanEntries.SingleAsync();
        remaining.FacilityId.Should().Be("F2");
    }

    [Fact]
    public async Task UpdateDoesNotCreateWhenTheEntryIsAbsent()
    {
        await _fixture.ResetAsync();

        var absent = Entry();
        absent.Id = Guid.NewGuid().ToString();

        var result = await WithServiceAsync(s => s.UpdateAsync(absent, CancellationToken.None));

        result.Should().BeNull();

        using var scope = _fixture.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportingPlanDbContext>();
        (await context.ReportingPlanEntries.CountAsync()).Should().Be(0, "update must never upsert");
    }
}
