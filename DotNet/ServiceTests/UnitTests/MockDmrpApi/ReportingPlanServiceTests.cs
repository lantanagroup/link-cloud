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

    private static ReportingPlanEntryEntity Entry(
        string facilityId = "F1",
        string measure = "HOB",
        int month = 5,
        int year = 2026,
        string isReporting = "Y",
        string? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Measure = measure,
            ReportingMonth = month,
            ReportingYear = year,
            IsReporting = isReporting,
            CreateDate = DateTime.UtcNow
        };

    // ------------------------------------------------------------ reporting plan

    [Fact]
    public async Task GetReportingPlanAsync_ReturnsOnlyTheMatchingFacilityMeasuresForThePeriod()
    {
        _repository.Seed(
            Entry(measure: "HOB"),
            Entry(measure: "HTCDI"),
            Entry(facilityId: "F2", measure: "HOB"),
            Entry(measure: "HOB", month: 6),
            Entry(measure: "HOB", year: 2025));

        var plan = await _service.GetReportingPlanAsync("F1", 5, 2026, CancellationToken.None);

        plan.Should().HaveCount(2);
        plan.Select(e => e.Measure).Should().BeEquivalentTo("HOB", "HTCDI");
    }

    [Fact]
    public async Task GetReportingPlanAsync_ExcludesEntriesNotBeingReported()
    {
        // Enrollment is conveyed by presence, so an entry marked as not reporting must not
        // appear in a plan -- it is equivalent to no entry at all.
        _repository.Seed(
            Entry(measure: "HOB"),
            Entry(measure: "HTCDI", isReporting: "N"));

        var plan = await _service.GetReportingPlanAsync("F1", 5, 2026, CancellationToken.None);

        plan.Should().ContainSingle();
        plan[0].Measure.Should().Be("HOB");
    }

    [Fact]
    public async Task GetReportingPlanAsync_ForAFacilityWithNoEntries_ReturnsEmptyRatherThanFailing()
    {
        _repository.Seed(Entry(facilityId: "F2"));

        var plan = await _service.GetReportingPlanAsync("unknown-facility", 5, 2026, CancellationToken.None);

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
