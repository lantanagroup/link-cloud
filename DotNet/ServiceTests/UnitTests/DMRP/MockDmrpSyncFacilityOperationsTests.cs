using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.MockDmrp;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP;

/// <summary>
/// With the Mock DMRP API switched on, the facility form's selection is the facility's enrollment, exactly as
/// it is the facility's schedule with DMRP off.
/// </summary>
[Trait("Category", "UnitTests")]
public class MockDmrpSyncFacilityOperationsTests
{
    private const string FacilityId = "100";
    private const string OtherFacilityId = "200";
    private const string DqmA = "dqm-A";
    private const string DqmB = "dqm-B";

    private static readonly ReportingPeriod Current = new(2026, 10);
    private static readonly ReportingPeriod Next = new(2026, 11);

    private readonly FakeMockDmrpServiceClient _mock = new();
    private readonly Mock<IFacilityOperations> _dmrpOperations = new();
    private readonly Mock<IEntityRepository<MeasureMapping>> _mappings = new();
    private readonly Mock<IFacilityReportingPeriodResolver> _periods = new();
    private readonly Mock<IFacilityExistence> _existence = new();
    private readonly Mock<IDmrpReportingPlanSync> _sync = new();
    private readonly Mock<IFacilityReportingPlanManager> _plans = new();

    public MockDmrpSyncFacilityOperationsTests()
    {
        // HOB and HOB2 share dqm-A, as two NHSN measures under one dQM do. ZZZ has no dQM yet, so the form
        // cannot show it and the write-through must never touch it.
        _mappings
            .Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new MeasureMapping { Measure = "HOB", DQM = DqmA, Frequency = Frequency.Monthly },
                new MeasureMapping { Measure = "HOB2", DQM = DqmA, Frequency = Frequency.Monthly },
                new MeasureMapping { Measure = "PSM", DQM = DqmB, Frequency = Frequency.Monthly },
                new MeasureMapping { Measure = "ZZZ", DQM = null, Frequency = Frequency.Adhoc }
            ]);

        _periods
            .Setup(p => p.Resolve(It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Current);
    }

    [Fact]
    public async Task CreateAsync_MappedSelection_EnrollsEveryMappedMeasureForBothPeriods()
    {
        var facility = Facility(monthly: [DqmA]);

        await CreateOperations().CreateAsync(facility);

        Assert.Equal(["MSC/HOB/Y", "MSC/HOB2/Y"], _mock.Measures(FacilityId, Current.Month, Current.Year));
        Assert.Equal(["MSC/HOB/Y", "MSC/HOB2/Y"], _mock.Measures(FacilityId, Next.Month, Next.Year));
    }

    /// <summary>
    /// The DMRP operations refuse a caller-supplied schedule and derive one from the plans, so the selection
    /// is handed on as an empty schedule. The DMRP create runs its own sync.
    /// </summary>
    [Fact]
    public async Task CreateAsync_MappedSelection_HandsOnAnEmptyScheduleWithoutSyncing()
    {
        var facility = Facility(monthly: [DqmA]);

        await CreateOperations().CreateAsync(facility);

        _dmrpOperations.Verify(o => o.CreateAsync(
            It.Is<FacilityModel>(f => f.ScheduledReports.Monthly.Length == 0 &&
                                      f.ScheduledReports.Weekly.Length == 0 &&
                                      f.ScheduledReports.Daily.Length == 0),
            It.IsAny<CancellationToken>()), Times.Once);
        _sync.VerifyNoOtherCalls();
    }

    /// <summary>
    /// A create that reuses a live facility's id is about to be refused by the host. Writing first would
    /// change the live facility's enrollment anyway.
    /// </summary>
    [Fact]
    public async Task CreateAsync_FacilityAlreadyExists_LeavesTheMockAlone()
    {
        _existence.Setup(e => e.ExistsAsync(FacilityId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mock.Seed(FacilityId, "MSC", "PSM", Current.Month, Current.Year);

        await CreateOperations().CreateAsync(Facility(monthly: [DqmA]));

        Assert.Equal(0, _mock.Writes);
        _dmrpOperations.Verify(o => o.CreateAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_UnmappedSelection_ThrowsBeforeWritingAnything()
    {
        var facility = Facility(monthly: [DqmA], weekly: [DqmA]);

        var ex = await Assert.ThrowsAsync<UnmappedDqmSelectionException>(
            () => CreateOperations().CreateAsync(facility));

        Assert.Contains("dqm-A (Weekly)", ex.Message);
        Assert.Equal(0, _mock.Writes);
        _dmrpOperations.VerifyNoOtherCalls();
    }

    /// <summary>
    /// The same as with DMRP off: a facility saved with no reports reports nothing, whatever was seeded.
    /// </summary>
    [Fact]
    public async Task CreateAsync_EmptySelection_RemovesManagedEntriesAndKeepsUnmappedOnes()
    {
        _mock.Seed(FacilityId, "MSC", "HOB", Current.Month, Current.Year);
        _mock.Seed(FacilityId, "MSC", "ZZZ", Current.Month, Current.Year);

        await CreateOperations().CreateAsync(Facility());

        Assert.Equal(["MSC/ZZZ/Y"], _mock.Measures(FacilityId, Current.Month, Current.Year));
    }

    /// <summary>
    /// A vendor-only edit re-sends the pre-filled selection. That must not churn the mock.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_UnchangedSelection_WritesNothing()
    {
        SeedBothPeriods(FacilityId, "MSC", "HOB", "HOB2");

        await Update(Facility(monthly: [DqmA]));

        Assert.Equal(0, _mock.Writes);
    }

    [Fact]
    public async Task UpdateAsync_DqmSwapped_ReplacesOnlyTheManagedMeasures()
    {
        SeedBothPeriods(FacilityId, "MSC", "HOB", "HOB2", "ZZZ");
        SeedBothPeriods(OtherFacilityId, "MSC", "HOB");
        _mock.Seed(FacilityId, "MSC", "HOB", 9, 2026);

        await Update(Facility(monthly: [DqmB]));

        Assert.Equal(["MSC/PSM/Y", "MSC/ZZZ/Y"], _mock.Measures(FacilityId, Current.Month, Current.Year));
        Assert.Equal(["MSC/PSM/Y", "MSC/ZZZ/Y"], _mock.Measures(FacilityId, Next.Month, Next.Year));
        Assert.Equal(["MSC/HOB/Y"], _mock.Measures(FacilityId, 9, 2026));
        Assert.Equal(["MSC/HOB/Y"], _mock.Measures(OtherFacilityId, Current.Month, Current.Year));
    }

    /// <summary>
    /// A measure already enrolled under PS is enrolled; adding an MSC copy would duplicate it.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_SelectedMeasureEnrolledUnderPs_KeepsItAsItIs()
    {
        SeedBothPeriods(FacilityId, "PS", "PSM");

        await Update(Facility(monthly: [DqmB]));

        Assert.Equal(0, _mock.Writes);
        Assert.Equal(["PS/PSM/Y"], _mock.Measures(FacilityId, Current.Month, Current.Year));
    }

    /// <summary>
    /// The mock serves only "Y" entries, so a kept measure whose entry says "N" is not enrolled until replaced.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_SelectedMeasureNotReporting_ReplacesItWithAReportingEntry()
    {
        _mock.Seed(FacilityId, "MSC", "PSM", Current.Month, Current.Year, isReporting: "N");

        await Update(Facility(monthly: [DqmB]));

        Assert.Equal(["MSC/PSM/Y"], _mock.Measures(FacilityId, Current.Month, Current.Year));
    }

    /// <summary>
    /// The DMRP operations build the schedule from stored plans, so the edit is synced in rather than left
    /// for the nightly job.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_SyncsTheCurrentPeriodBeforeHandingOn()
    {
        var sequence = new MockSequence();
        _sync.InSequence(sequence)
            .Setup(s => s.SyncAsync(FacilityId, Current.Month, Current.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DmrpSyncResult.Nothing);
        _dmrpOperations.InSequence(sequence)
            .Setup(o => o.UpdateAsync(It.IsAny<FacilityModel>(), It.IsAny<FacilityModel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Update(Facility(monthly: [DqmA]));

        _sync.Verify(s => s.SyncAsync(FacilityId, Current.Month, Current.Year, It.IsAny<CancellationToken>()),
            Times.Once);
        _dmrpOperations.Verify(o => o.UpdateAsync(It.IsAny<FacilityModel>(),
            It.Is<FacilityModel>(f => f.ScheduledReports.Monthly.Length == 0), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The sync writes nothing when DMRP returns nothing, so clearing every report would otherwise leave the
    /// old plans reporting - this month and, once recorded, next month too.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_EmptySelection_WithdrawsEveryManagedPlanInBothPeriods()
    {
        SeedBothPeriods(FacilityId, "MSC", "HOB", "HOB2");

        await Update(Facility());

        Assert.Empty(_mock.Measures(FacilityId, Current.Month, Current.Year));
        _plans.Verify(p => p.WithdrawUnselectedAsync(FacilityId,
            It.Is<IReadOnlyCollection<ReportingPeriod>>(periods => periods.SequenceEqual(new[] { Current, Next })),
            It.Is<IReadOnlySet<string>>(keep => keep.Count == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SelectionKept_WithdrawsOnlyWhatItDropped()
    {
        await Update(Facility(monthly: [DqmA]));

        _plans.Verify(p => p.WithdrawUnselectedAsync(FacilityId, It.IsAny<IReadOnlyCollection<ReportingPeriod>>(),
            It.Is<IReadOnlySet<string>>(keep => keep.SetEquals(new[] { "HOB", "HOB2" })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(503)]
    public async Task UpdateAsync_MockFails_ThrowsDmrpApiExceptionAndSavesNothing(int status)
    {
        _mock.FailWith = status;

        await Assert.ThrowsAsync<DmrpApiException>(() => Update(Facility(monthly: [DqmA])));

        _sync.VerifyNoOtherCalls();
        _plans.VerifyNoOtherCalls();
        _dmrpOperations.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_ClearsTheFacilitysEntriesAfterTheDelete()
    {
        SeedBothPeriods(FacilityId, "MSC", "HOB");
        SeedBothPeriods(OtherFacilityId, "MSC", "HOB");

        await CreateOperations().DeleteAsync(FacilityId);

        _dmrpOperations.Verify(o => o.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(_mock.Entries, e => e.FacilityId == FacilityId);
        Assert.Contains(_mock.Entries, e => e.FacilityId == OtherFacilityId);
    }

    /// <summary>
    /// The facility is already gone, so a mock failure must not turn a successful delete into an error.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_MockFails_StillSucceeds()
    {
        _mock.FailWith = 0;

        await CreateOperations().DeleteAsync(FacilityId);

        _dmrpOperations.Verify(o => o.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_HostRefuses_LeavesTheMockAlone()
    {
        SeedBothPeriods(FacilityId, "MSC", "HOB");
        _dmrpOperations
            .Setup(o => o.DeleteAsync(FacilityId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApplicationException("refused"));

        await Assert.ThrowsAsync<ApplicationException>(() => CreateOperations().DeleteAsync(FacilityId));

        Assert.False(_mock.DeletedByFacility);
    }

    [Fact]
    public async Task SoftDeleteAndRestore_LeaveTheMockAlone()
    {
        SeedBothPeriods(FacilityId, "MSC", "HOB");
        var operations = CreateOperations();

        await operations.SoftDeleteAsync(FacilityId);
        await operations.RestoreAsync(Facility());

        Assert.Equal(0, _mock.Writes);
        Assert.False(_mock.DeletedByFacility);
        _dmrpOperations.Verify(o => o.SoftDeleteAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
        _dmrpOperations.Verify(o => o.RestoreAsync(It.IsAny<FacilityModel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private MockDmrpSyncFacilityOperations CreateOperations() =>
        new(NullLogger<MockDmrpSyncFacilityOperations>.Instance,
            _dmrpOperations.Object,
            _mock,
            _mappings.Object,
            _periods.Object,
            _existence.Object,
            _sync.Object,
            _plans.Object);

    private Task Update(FacilityModel updated) => CreateOperations().UpdateAsync(Facility(), updated);

    private void SeedBothPeriods(string facilityId, string component, params string[] measures)
    {
        foreach (var period in new[] { Current, Next })
        {
            foreach (var measure in measures)
            {
                _mock.Seed(facilityId, component, measure, period.Month, period.Year);
            }
        }
    }

    private static FacilityModel Facility(string[]? monthly = null, string[]? weekly = null) => new()
    {
        FacilityId = FacilityId,
        FacilityName = "Facility",
        TimeZone = "America/Chicago",
        ScheduledReports = new TenantScheduledReportConfig
        {
            Daily = [],
            Weekly = weekly ?? [],
            Monthly = monthly ?? []
        }
    };
}
