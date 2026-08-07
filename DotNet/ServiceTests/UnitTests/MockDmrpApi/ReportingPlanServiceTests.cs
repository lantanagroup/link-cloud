using FluentAssertions;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Application.Services;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using Xunit;

// ServiceTests globally imports Hl7.Fhir.Model, which has its own Task type.
using Task = System.Threading.Tasks.Task;

namespace UnitTests.MockDmrpApi;

public class ReportingPlanServiceTests
{
    private readonly FakeEntryRepository _repository = new();
    private readonly ReportingPlanService _service;

    public ReportingPlanServiceTests()
    {
        _service = new ReportingPlanService(_repository);
    }

    /// <summary>
    /// A monthly (MSC) entry. The default for this service's most common shape.
    /// </summary>
    private static ReportingPlanEntryEntity Entry(
        string facilityId = "F1",
        string measure = "HOB",
        int? month = 5,
        int year = 2026,
        string isReporting = "Y",
        string component = ReportingComponents.Msc,
        string? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Component = component,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting,
            CreateDate = DateTime.UtcNow
        };

    /// <summary>An annual (PS) entry, which carries no reporting month.</summary>
    private static ReportingPlanEntryEntity AnnualEntry(
        string facilityId = "F1",
        string measure = "HAI",
        int year = 2026,
        string isReporting = "Y",
        string? id = null) =>
        Entry(facilityId, measure, month: null, year, isReporting, ReportingComponents.Ps, id);

    // -------------------------------------------------------- monthly plan (MSC)

    [Fact]
    public async Task GetReportingPlanAsync_Monthly_ReturnsOnlyTheMatchingFacilityMeasuresForThePeriod()
    {
        _repository.Seed(
            Entry(measure: "HOB"),
            Entry(measure: "HTCDI"),
            Entry(facilityId: "F2", measure: "HOB"),
            Entry(measure: "HOB", month: 6),
            Entry(measure: "HOB", year: 2025));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Msc, "F1", null, 5, 2026, CancellationToken.None);

        plan.Should().HaveCount(2);
        plan.Select(e => e.Measure).Should().BeEquivalentTo("HOB", "HTCDI");
    }

    [Fact]
    public async Task GetReportingPlanAsync_Monthly_ExcludesOtherComponents()
    {
        // The two endpoints share one table. A patient-safety measure appearing in the
        // medicine plan would be a silent cross-contamination, not a visible failure.
        _repository.Seed(
            Entry(measure: "HOB"),
            Entry(measure: "HAI", component: ReportingComponents.Ps, month: 5));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Msc, "F1", null, 5, 2026, CancellationToken.None);

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetReportingPlanAsync_Monthly_ExcludesEntriesNotBeingReported()
    {
        // Enrollment is conveyed by presence, so an entry marked as not reporting must not
        // appear in a plan -- it is equivalent to no entry at all.
        _repository.Seed(
            Entry(measure: "HOB"),
            Entry(measure: "HTCDI", isReporting: "N"));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Msc, "F1", null, 5, 2026, CancellationToken.None);

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetReportingPlanAsync_Monthly_ForAFacilityWithNoEntries_ReturnsEmptyRatherThanFailing()
    {
        _repository.Seed(Entry(facilityId: "F2"));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Msc, "unknown-facility", null, 5, 2026, CancellationToken.None);

        plan.Should().BeEmpty();
    }

    // --------------------------------------------------------- annual plan (PS)

    [Fact]
    public async Task GetReportingPlanAsync_Annual_ReturnsEveryMeasureForTheYearRegardlessOfMonth()
    {
        // The annual predicate deliberately omits month. Seeding an MSC entry in the same
        // year proves the omission does not widen the result to other components.
        _repository.Seed(
            AnnualEntry(measure: "HAI"),
            AnnualEntry(measure: "SSI"),
            AnnualEntry(measure: "HAI", year: 2025),
            AnnualEntry(facilityId: "F2", measure: "HAI"),
            Entry(measure: "HOB"));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Ps, "F1", null, null, 2026, CancellationToken.None);

        plan.Should().HaveCount(2);
        plan.Select(e => e.Measure).Should().BeEquivalentTo("HAI", "SSI");
        plan.Should().OnlyContain(e => e.ReportingMonth == null);
    }

    [Fact]
    public async Task GetReportingPlanAsync_Annual_ExcludesEntriesNotBeingReported()
    {
        _repository.Seed(
            AnnualEntry(measure: "HAI"),
            AnnualEntry(measure: "SSI", isReporting: "N"));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Ps, "F1", null, null, 2026, CancellationToken.None);

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HAI");
    }

    [Fact]
    public async Task GetReportingPlanAsync_Annual_ForAFacilityWithNoEntries_ReturnsEmptyRatherThanFailing()
    {
        _repository.Seed(AnnualEntry(facilityId: "F2"));

        var plan = await _service.GetReportingPlanAsync(
            ReportingComponents.Ps, "unknown-facility", null, null, 2026, CancellationToken.None);

        plan.Should().BeEmpty();
    }

    // -------------------------------------------------------------------- search

    [Fact]
    public async Task SearchAsync_WithNoFilters_ReturnsEverything()
    {
        _repository.Seed(Entry(), Entry(measure: "HTCDI"), Entry(facilityId: "F2"));

        var (records, metadata) = await _service.SearchAsync(new ReportingPlanSearchCriteria(), CancellationToken.None);

        records.Should().HaveCount(3);
        metadata.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task SearchAsync_FiltersByFacility()
    {
        _repository.Seed(Entry(), Entry(facilityId: "F2"), Entry(facilityId: "F2", measure: "HTCDI"));

        var (records, _) = await _service.SearchAsync(
            new ReportingPlanSearchCriteria { FacilityId = "F2" }, CancellationToken.None);

        records.Should().HaveCount(2);
        records.Should().OnlyContain(e => e.FacilityId == "F2");
    }

    [Fact]
    public async Task SearchAsync_FiltersByComponent()
    {
        _repository.Seed(Entry(measure: "HOB"), AnnualEntry(measure: "HAI"));

        var (records, _) = await _service.SearchAsync(
            new ReportingPlanSearchCriteria { Component = ReportingComponents.Ps }, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Measure.Should().Be("HAI");
    }

    [Fact]
    public async Task SearchAsync_MatchesMeasureCaseInsensitively()
    {
        _repository.Seed(Entry(measure: "HOB"), Entry(measure: "HTCDI"));

        var (records, _) = await _service.SearchAsync(
            new ReportingPlanSearchCriteria { Measure = "hob" }, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task SearchAsync_CombinesFiltersConjunctively()
    {
        _repository.Seed(
            Entry(measure: "HOB", month: 5, year: 2026),
            Entry(measure: "HOB", month: 6, year: 2026),
            Entry(measure: "HTCDI", month: 5, year: 2026),
            Entry(facilityId: "F2", measure: "HOB", month: 5, year: 2026));

        var (records, _) = await _service.SearchAsync(
            new ReportingPlanSearchCriteria
            {
                FacilityId = "F1",
                Component = ReportingComponents.Msc,
                Measure = "HOB",
                ReportingMonth = 5,
                ReportingYear = 2026
            },
            CancellationToken.None);

        records.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_FiltersByIsReporting()
    {
        _repository.Seed(Entry(isReporting: "Y"), Entry(measure: "HTCDI", isReporting: "N"));

        var (records, _) = await _service.SearchAsync(
            new ReportingPlanSearchCriteria { IsReporting = "N" }, CancellationToken.None);

        records.Should().ContainSingle();
        records[0].Measure.Should().Be("HTCDI");
    }

    [Theory]
    [InlineData(ReportingPlanSortBy.FacilityId, "FacilityId")]
    [InlineData(ReportingPlanSortBy.Component, "Component")]
    [InlineData(ReportingPlanSortBy.Measure, "Measure")]
    [InlineData(ReportingPlanSortBy.ReportingMonth, "ReportingMonth")]
    [InlineData(ReportingPlanSortBy.ReportingYear, "ReportingYear")]
    [InlineData(ReportingPlanSortBy.CreateDate, "CreateDate")]
    [InlineData(ReportingPlanSortBy.ModifyDate, "ModifyDate")]
    public async Task SearchAsync_ResolvesEverySortFieldToARealEntityProperty(ReportingPlanSortBy sortBy, string expected)
    {
        // Every enum member must map to an actual property. The shared repository builds
        // the sort expression by name and throws for anything that is not one, so a typo
        // here would be a 500 from user input rather than a compile error.
        _repository.Seed(Entry());

        await _service.SearchAsync(new ReportingPlanSearchCriteria { SortBy = sortBy }, CancellationToken.None);

        _repository.LastSortBy.Should().Be(expected);
        typeof(ReportingPlanEntryEntity).GetProperty(expected).Should().NotBeNull();
    }

    [Fact]
    public async Task SearchAsync_WithSortFieldOutsideTheEnum_ThrowsRatherThanReachingTheRepository()
    {
        var criteria = new ReportingPlanSearchCriteria { SortBy = (ReportingPlanSortBy)999 };

        var act = async () => await _service.SearchAsync(criteria, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        _repository.LastSortBy.Should().BeNull();
    }

    [Fact]
    public async Task SearchAsync_DefaultsToNewestFirst()
    {
        _repository.Seed(Entry());

        await _service.SearchAsync(new ReportingPlanSearchCriteria(), CancellationToken.None);

        _repository.LastSortBy.Should().Be(nameof(ReportingPlanEntryEntity.CreateDate));
        _repository.LastSortOrder.Should().Be(SortOrder.Descending);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(5000, 100)]
    public async Task SearchAsync_ClampsPageSize(int requested, int expected)
    {
        await _service.SearchAsync(
            new ReportingPlanSearchCriteria { PageSize = requested }, CancellationToken.None);

        _repository.LastPageSize.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public async Task SearchAsync_ClampsPageNumber(int requested, int expected)
    {
        await _service.SearchAsync(
            new ReportingPlanSearchCriteria { PageNumber = requested }, CancellationToken.None);

        _repository.LastPageNumber.Should().Be(expected);
    }

    [Fact]
    public async Task SearchAsync_WithNullCriteria_Throws()
    {
        var act = async () => await _service.SearchAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --------------------------------------------------------------- by facility

    [Fact]
    public async Task GetByFacilityAsync_ReturnsOnlyThatFacilityAndClampsPaging()
    {
        _repository.Seed(Entry(), Entry(measure: "HTCDI"), Entry(facilityId: "F2"));

        var (records, metadata) = await _service.GetByFacilityAsync("F1", 0, 0, CancellationToken.None);

        records.Should().HaveCount(2);
        records.Should().OnlyContain(e => e.FacilityId == "F1");
        metadata.TotalCount.Should().Be(2);
        _repository.LastPageSize.Should().Be(10);
        _repository.LastPageNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetByFacilityAsync_SpansComponents()
    {
        // The support surface is for inspecting what was seeded, so it should show a
        // facility's entries whatever component they belong to.
        _repository.Seed(Entry(measure: "HOB"), AnnualEntry(measure: "HAI"));

        var (records, _) = await _service.GetByFacilityAsync("F1", 10, 1, CancellationToken.None);

        records.Should().HaveCount(2);
    }

    // ------------------------------------------------------------- trimming

    [Fact]
    public async Task CreateAsync_TrimsEveryFieldThatTakesPartInTheNaturalKey()
    {
        var created = await _service.CreateAsync(
            new ReportingPlanEntryEntity
            {
                FacilityId = "  F1  ",
                Component = "  MSC  ",
                Measure = "  HOB  ",
                ReportingMonth = 5,
                ReportingYear = 2026,
                IsReporting = " Y "
            },
            CancellationToken.None);

        created.FacilityId.Should().Be("F1");
        created.Component.Should().Be("MSC");
        created.Measure.Should().Be("HOB");
        created.IsReporting.Should().Be("Y");
    }

    [Fact]
    public async Task CreateAsync_TreatsAPaddedMeasureAsADuplicateOfItsTrimmedTwin()
    {
        // Before trimming these were two rows, and the padded one was invisible to a plan
        // query -- a silent short plan rather than a visible error.
        _repository.Seed(Entry(measure: "HOB"));

        var act = async () => await _service.CreateAsync(
            Entry(measure: " HOB "), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateReportingPlanEntryException>();
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_WithAWhitespaceOnlyMeasure_IsRejected()
    {
        // Trimming turns "   " into "", which the cadence guard would let through -- an entry
        // with no measure at all. The measure has to survive the trim.
        var act = async () => await _service.CreateAsync(
            Entry(measure: "   "), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidReportingPlanEntryException>();
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportingPlanAsync_MatchesAPaddedQueryAgainstATrimmedRow()
    {
        _repository.Seed(Entry(measure: "HOB"));

        var plan = await _service.GetReportingPlanAsync(
            "  MSC  ", "  F1  ", "  HOB  ", 5, 2026, CancellationToken.None);

        plan.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_MatchesAPaddedFilterAgainstATrimmedRow()
    {
        _repository.Seed(Entry(measure: "HOB"));

        var (records, _) = await _service.SearchAsync(
            new ReportingPlanSearchCriteria { FacilityId = " F1 ", Measure = " HOB " },
            CancellationToken.None);

        records.Should().ContainSingle();
    }

    // ---------------------------------------------- component and period rules

    [Fact]
    public async Task CreateAsync_ForAMonthlyComponentWithNoMonth_Throws()
    {
        // Saved, this row would match no month, so it would be invisible to /msc forever.
        var act = async () => await _service.CreateAsync(
            Entry(month: null), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidReportingPlanEntryException>())
            .WithMessage("*monthly*");
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ForAnAnnualComponentWithAMonth_Throws()
    {
        // The mirror failure: /ps/annual does not filter on month, so a stray month makes
        // the row appear in the annual plan while looking like a monthly entry in storage.
        var entry = AnnualEntry();
        entry.ReportingMonth = 5;

        var act = async () => await _service.CreateAsync(entry, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidReportingPlanEntryException>())
            .WithMessage("*annually*");
        _repository.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task CreateAsync_WithAMonthOutsideTheYear_Throws(int month)
    {
        var act = async () => await _service.CreateAsync(Entry(month: month), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidReportingPlanEntryException>();
        _repository.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("XYZ")]
    [InlineData("MSCX")]
    public async Task CreateAsync_WithAnUnrecognisedComponent_Throws(string component)
    {
        var act = async () => await _service.CreateAsync(
            Entry(component: component), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidReportingPlanEntryException>();
        _repository.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData("msc")]
    [InlineData("Msc")]
    [InlineData("MSC")]
    public async Task CreateAsync_AcceptsAKnownComponentInAnyCasing(string component)
    {
        await _service.CreateAsync(Entry(component: component), CancellationToken.None);

        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateAsync_AppliesTheSamePeriodRulesAsCreate()
    {
        var existing = Entry();
        _repository.Seed(existing);

        var act = async () => await _service.UpdateAsync(
            new ReportingPlanEntryEntity
            {
                Id = existing.Id,
                FacilityId = "F1",
                Component = ReportingComponents.Ps,
                Measure = "HAI",
                ReportingMonth = 5,
                ReportingYear = 2026,
                IsReporting = "Y"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidReportingPlanEntryException>();
        _repository.Entries[0].Component.Should().Be(ReportingComponents.Msc, "the update must not partially apply");
    }

    // -------------------------------------------------------------------- create

    [Fact]
    public async Task CreateAsync_AssignsAnIdentifierAndPersists()
    {
        var created = await _service.CreateAsync(Entry(id: string.Empty), CancellationToken.None);

        created.Id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(created.Id, out _).Should().BeTrue();
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateNaturalKey_Throws()
    {
        _repository.Seed(Entry());

        var act = async () => await _service.CreateAsync(Entry(), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateReportingPlanEntryException>();
        _repository.Entries.Should().ContainSingle("the duplicate must not be persisted");
    }

    [Fact]
    public async Task CreateAsync_ForAnAnnualComponent_StillRejectsADuplicate()
    {
        // Annual entries have a null month, and NULL compares as a value in the unique
        // index. If the pre-check treated null as "no constraint" the duplicate would slip
        // through to the database and surface as a 500 instead of a 409.
        _repository.Seed(AnnualEntry());

        var act = async () => await _service.CreateAsync(AnnualEntry(), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateReportingPlanEntryException>();
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateAsync_AllowsTheSameMeasureUnderADifferentComponent()
    {
        // The two components are independent plans, so the same measure name in each is a
        // legitimate pair of rows rather than a duplicate.
        _repository.Seed(Entry(measure: "SHARED"));

        await _service.CreateAsync(AnnualEntry(measure: "SHARED"), CancellationToken.None);

        _repository.Entries.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_AllowsSameMeasureForADifferentPeriodOrFacility()
    {
        _repository.Seed(Entry());

        await _service.CreateAsync(Entry(month: 6), CancellationToken.None);
        await _service.CreateAsync(Entry(year: 2027), CancellationToken.None);
        await _service.CreateAsync(Entry(facilityId: "F2"), CancellationToken.None);

        _repository.Entries.Should().HaveCount(4);
    }

    // -------------------------------------------------------------------- update

    [Fact]
    public async Task UpdateAsync_ForAMissingEntry_ReturnsNullAndCreatesNothing()
    {
        // Update is update-only. Silently upserting would let a caller create entries
        // through a verb that promises not to.
        var result = await _service.UpdateAsync(Entry(id: Guid.NewGuid().ToString()), CancellationToken.None);

        result.Should().BeNull();
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_AppliesEveryMutableField()
    {
        var existing = Entry();
        _repository.Seed(existing);

        var result = await _service.UpdateAsync(
            new ReportingPlanEntryEntity
            {
                Id = existing.Id,
                FacilityId = "F9",
                Component = ReportingComponents.Msc,
                Measure = "HTCDI",
                ReportingMonth = 11,
                ReportingYear = 2027,
                IsReporting = "N"
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.FacilityId.Should().Be("F9");
        result.Measure.Should().Be("HTCDI");
        result.ReportingMonth.Should().Be(11);
        result.ReportingYear.Should().Be(2027);
        result.IsReporting.Should().Be("N");
    }

    [Fact]
    public async Task UpdateAsync_CanMoveAnEntryBetweenComponents()
    {
        // Changing the component also changes the cadence, so the month has to go with it.
        var existing = Entry();
        _repository.Seed(existing);

        var result = await _service.UpdateAsync(
            new ReportingPlanEntryEntity
            {
                Id = existing.Id,
                FacilityId = existing.FacilityId,
                Component = ReportingComponents.Ps,
                Measure = existing.Measure,
                ReportingMonth = null,
                ReportingYear = existing.ReportingYear,
                IsReporting = "Y"
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Component.Should().Be(ReportingComponents.Ps);
        result.ReportingMonth.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ThatWouldCollideWithAnotherEntry_Throws()
    {
        var first = Entry(measure: "HOB");
        var second = Entry(measure: "HTCDI");
        _repository.Seed(first, second);

        // Move the second entry onto the first entry's natural key.
        var act = async () => await _service.UpdateAsync(
            new ReportingPlanEntryEntity
            {
                Id = second.Id,
                FacilityId = "F1",
                Component = ReportingComponents.Msc,
                Measure = "HOB",
                ReportingMonth = 5,
                ReportingYear = 2026,
                IsReporting = "Y"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateReportingPlanEntryException>();
    }

    [Fact]
    public async Task UpdateAsync_LeavingTheNaturalKeyUnchanged_DoesNotCollideWithItself()
    {
        var existing = Entry();
        _repository.Seed(existing);

        var result = await _service.UpdateAsync(
            new ReportingPlanEntryEntity
            {
                Id = existing.Id,
                FacilityId = existing.FacilityId,
                Component = existing.Component,
                Measure = existing.Measure,
                ReportingMonth = existing.ReportingMonth,
                ReportingYear = existing.ReportingYear,
                IsReporting = "N"
            },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.IsReporting.Should().Be("N");
    }

    // -------------------------------------------------------------------- delete

    [Fact]
    public async Task DeleteAsync_RemovesTheEntryAndReportsWhetherItExisted()
    {
        var existing = Entry();
        _repository.Seed(existing);

        (await _service.DeleteAsync(existing.Id, CancellationToken.None)).Should().BeTrue();
        _repository.Entries.Should().BeEmpty();

        (await _service.DeleteAsync(existing.Id, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteByFacilityAsync_RemovesOnlyThatFacility()
    {
        _repository.Seed(Entry(), Entry(measure: "HTCDI"), Entry(facilityId: "F2"));

        var removed = await _service.DeleteByFacilityAsync("F1", CancellationToken.None);

        removed.Should().Be(2);
        _repository.Entries.Should().ContainSingle();
        _repository.Entries[0].FacilityId.Should().Be("F2");
    }

    [Fact]
    public async Task DeleteByFacilityAsync_RemovesEveryComponentForThatFacility()
    {
        // A teardown between test runs has to leave nothing behind, so it cannot be
        // component-scoped.
        _repository.Seed(Entry(), AnnualEntry(), Entry(facilityId: "F2"));

        var removed = await _service.DeleteByFacilityAsync("F1", CancellationToken.None);

        removed.Should().Be(2);
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteByFacilityAsync_ForAFacilityWithNoEntries_IsANoOp()
    {
        _repository.Seed(Entry());

        var removed = await _service.DeleteByFacilityAsync("nobody", CancellationToken.None);

        removed.Should().Be(0);
        _repository.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteAllAsync_EmptiesTheStore()
    {
        _repository.Seed(Entry(), Entry(measure: "HTCDI"), Entry(facilityId: "F2"));

        var removed = await _service.DeleteAllAsync(CancellationToken.None);

        removed.Should().Be(3);
        _repository.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAllAsync_OnAnEmptyStore_IsANoOp()
    {
        var removed = await _service.DeleteAllAsync(CancellationToken.None);

        removed.Should().Be(0);
    }
}
