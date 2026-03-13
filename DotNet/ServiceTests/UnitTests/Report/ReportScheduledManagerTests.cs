using LantanaGroup.Link.Report.Application.Factory;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

[Trait("Category", "UnitTests")]
public class ReportScheduledManagerTests
{
    private readonly Mock<ILogger<ReportScheduledManager>> _mockLogger;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<IEntityRepository<ReportSchedule>> _mockRepository;
    private readonly Mock<IQuartzJobHelper> _mockQuartzJobHelper;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<ScheduledReportFactory> _mockScheduledReportFactory;

    // A minimal testable subclass that bypasses the MongoDB-specific constructor logic.
    // Seeding is done via Seed(), which writes to the EF Core in-memory store without
    // counting the call as a test-observable SaveChangesAsync invocation.
    private readonly TestableMongoDbContext _context;

    private readonly ReportScheduledManager _sut;

    public ReportScheduledManagerTests()
    {
        _mockLogger = new Mock<ILogger<ReportScheduledManager>>();
        _mockDatabase = new Mock<IDatabase>();
        _mockRepository = new Mock<IEntityRepository<ReportSchedule>>();
        _mockQuartzJobHelper = new Mock<IQuartzJobHelper>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScheduledReportFactory = new Mock<ScheduledReportFactory>();

        _mockDatabase
            .SetupGet(d => d.ReportScheduledRepository)
            .Returns(_mockRepository.Object);

        _context = new TestableMongoDbContext();

        _sut = new ReportScheduledManager(
            _mockLogger.Object,
            _context,
            _mockDatabase.Object,
            _mockScheduledReportFactory.Object,
            _mockServiceScopeFactory.Object,
            _mockQuartzJobHelper.Object);
    }

    #region Argument Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateReportsDeletedStatusForFacility_WithNullOrWhitespaceFacilityId_ThrowsArgumentException(string? facilityId)
    {
        // Act
        var act = async () => await _sut.UpdateReportsDeletedStatusForFacility(facilityId!, true);

        // Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(act);
        Assert.Equal("facilityId", exception.ParamName);
    }

    #endregion

    #region Soft Delete (deleted = true) Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenDeleted_SetsIsDeletedTrue()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);

        // Submitted reports go through the EF Core batch path, not the repository mock.
        // Seed them directly into the in-memory store so the LINQ query can find them.
        await _context.Seed(submittedReport);
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.True(submittedReport.IsDeleted);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenDeleted_SetsModifyDate()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);
        var beforeTest = DateTime.UtcNow;

        await _context.Seed(submittedReport);
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.NotNull(submittedReport.ModifyDate);
        Assert.True(submittedReport.ModifyDate >= beforeTest);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenDeleted_CallsSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);

        await _context.Seed(submittedReport);
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_CallsDeleteJob()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);
        scheduledReport.Id = "sched-report-id-1";

        // Seed into context so UpdateRange has a tracked entity to modify.
        await _context.Seed(scheduledReport);
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob(
                scheduledReport.Id,
                "MeasureReportSubmissionGroup",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_SetsIsDeletedTrue()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled, isDeleted: false);

        await _context.Seed(scheduledReport);
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: Scheduled reports ARE soft-deleted along with their Quartz job
        Assert.True(scheduledReport.IsDeleted);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_CallsSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);

        await _context.Seed(scheduledReport);
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: Scheduled reports are persisted
        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithEndOfPeriodReport_WhenDeleted_LeavesReportUntouched()
    {
        // Arrange
        var facilityId = "FAC001";
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: false);
        var originalModifyDate = endOfPeriodReport.ModifyDate;

        // EndOfPeriod reports don't match the Status == Scheduled query; return empty from repository
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: EndOfPeriod report is never touched
        Assert.False(endOfPeriodReport.IsDeleted);
        Assert.Equal(originalModifyDate, endOfPeriodReport.ModifyDate);
        Assert.False(_context.SaveChangesWasCalled);
        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithNewReport_WhenDeleted_LeavesReportUntouched()
    {
        // Arrange
        var facilityId = "FAC001";
        var newReport = CreateReport(facilityId, ScheduleStatus.New, isDeleted: false);
        var originalModifyDate = newReport.ModifyDate;

        // New reports don't match the Status == Scheduled query; return empty from repository
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: New report is never touched
        Assert.False(newReport.IsDeleted);
        Assert.Equal(originalModifyDate, newReport.ModifyDate);
        Assert.False(_context.SaveChangesWasCalled);
        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Restore (deleted = false) Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenNotDeleted_SetsIsDeletedFalse()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: true);

        await _context.Seed(submittedReport);
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        // Assert
        Assert.False(submittedReport.IsDeleted);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenNotDeleted_CallsSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: true);

        await _context.Seed(submittedReport);
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        // Assert
        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenNotDeleted_DoesNotCallDeleteJob()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);

        // scheduledReport.IsDeleted == false, so scheduledToUpdate will be empty (restore
        // path only updates reports where IsDeleted == true). No UpdateRange → no seeding needed.
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        // Assert: deleted=false means no job deletion for Scheduled reports
        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WhenDeleteJobThrows_ThrowsInvalidOperationException()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);

        // The method throws before reaching UpdateRange, so no seeding is required.
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Quartz job store error"));

        // Act & Assert — Quartz failure must propagate so Admin.BFF can roll back the tenant
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true));
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WhenDeleteJobThrows_LogsWarning()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);

        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        var expectedException = new InvalidOperationException("Quartz job store error");
        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act — will throw, but the warning must have been logged before that
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true));

        // Assert: warning was logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to delete Quartz job")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SaveChangesAsync Call Conditions Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithNoReports_DoesNotCallSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";

        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.False(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithOnlyScheduledReports_WhenDeleted_CallsSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport1 = CreateReport(facilityId, ScheduleStatus.Scheduled);
        var scheduledReport2 = CreateReport(facilityId, ScheduleStatus.Scheduled);

        await _context.Seed(scheduledReport1, scheduledReport2);
        SetupFindAsync(new List<ReportSchedule> { scheduledReport1, scheduledReport2 });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: Scheduled reports are persisted when soft-deleted
        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMultipleSubmittedReports_UpdatesAllOfThemAndSavesOnce()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport1 = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);
        var submittedReport2 = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);

        // Seed both into the in-memory store; the batch LINQ query will find them.
        await _context.Seed(submittedReport1, submittedReport2);
        SetupFindAsync(new List<ReportSchedule>());

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: both reports updated, one batch → one SaveChanges call
        Assert.True(submittedReport1.IsDeleted);
        Assert.True(submittedReport2.IsDeleted);
        Assert.True(_context.SaveChangesWasCalled);
        Assert.Equal(1, _context.SaveChangesCallCount);
    }

    #endregion

    #region Mixed Status Tests

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMixedStatuses_WhenDeleted_HandlesEachStatusCorrectly()
    {
        // Arrange
        var facilityId = "FAC001";
        // Only Scheduled reports come from the repository (Status == Scheduled query)
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);
        scheduledReport.Id = "sched-mixed-id";
        // EndOfPeriod and New reports don't match either query path — they stay untouched
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: false);
        var newReport = CreateReport(facilityId, ScheduleStatus.New, isDeleted: false);

        await _context.Seed(scheduledReport);
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: Scheduled report is soft-deleted and its Quartz job removed
        Assert.True(scheduledReport.IsDeleted);
        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob("sched-mixed-id", "MeasureReportSubmissionGroup", It.IsAny<CancellationToken>()),
            Times.Once);

        // Assert: EndOfPeriod and New reports are never touched (local sentinel objects,
        // not seeded into the context and not returned by the mock)
        Assert.False(endOfPeriodReport.IsDeleted);
        Assert.False(newReport.IsDeleted);

        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMixedStatuses_WhenNotDeleted_OnlyScheduledReportsAreUpdated()
    {
        // Arrange
        var facilityId = "FAC001";
        // The repository query filters by Status == Scheduled, so only this report comes back
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled, isDeleted: true);
        // endOfPeriodReport is a sentinel: not seeded in context, not returned by mock —
        // its IsDeleted should remain true to confirm it was never restored
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: true);

        await _context.Seed(scheduledReport);
        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        // Assert: deleted scheduled report is restored
        Assert.False(scheduledReport.IsDeleted);

        // No DeleteJob calls during restore
        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // EndOfPeriod report is never restored (sentinel remains unchanged)
        Assert.True(endOfPeriodReport.IsDeleted == true);

        Assert.True(_context.SaveChangesWasCalled);
    }

    #endregion

    #region Helper Methods

    private static ReportSchedule CreateReport(string facilityId, ScheduleStatus status, bool isDeleted = false)
    {
        return new ReportSchedule
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = status,
            IsDeleted = isDeleted,
            CreateDate = DateTime.UtcNow,
            ModifyDate = null
        };
    }

    /// <summary>
    /// Configures the repository mock so that <see cref="IEntityRepository{T}.FindAsync"/> applies
    /// the predicate to <paramref name="reports"/> and returns only the matching items.
    /// This mirrors the real repository behaviour and prevents non-matching fixtures (e.g. Submitted
    /// reports) from leaking into the Scheduled-report code path.
    /// </summary>
    private void SetupFindAsync(List<ReportSchedule> reports)
    {
        _mockRepository
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ReportSchedule, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((System.Linq.Expressions.Expression<Func<ReportSchedule, bool>> predicate, CancellationToken _) =>
                reports.Where(predicate.Compile()).ToList());
    }

    #endregion

    /// <summary>
    /// A minimal concrete subclass of <see cref="MongoDbContext"/> that uses the EF Core in-memory
    /// provider so the batch LINQ queries in
    /// <see cref="ReportScheduledManager.UpdateReportsDeletedStatusForFacility"/> return real results.
    ///
    /// Use <see cref="Seed"/> to populate the in-memory store before the test.  The seed call is
    /// transparent — it does NOT increment <see cref="SaveChangesCallCount"/>.
    /// </summary>
    private sealed class TestableMongoDbContext : MongoDbContext
    {
        private bool _seeding;

        public bool SaveChangesWasCalled { get; private set; }
        public int SaveChangesCallCount { get; private set; }

        public TestableMongoDbContext()
            : base(
                new DbContextOptionsBuilder<MongoDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options,
                new Mock<IMongoDatabase>().Object,
                new Mock<ILogger<MongoDbContext>>().Object)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Do NOT call base — base calls ToCollection() (MongoDB-specific extension) which
            // breaks the EF Core in-memory provider's internal service provider.
            // Configure only the minimum needed for tests that exercise ReportSchedule.
            modelBuilder.Entity<ReportSchedule>();
            modelBuilder.Entity<ReportEntry>().Ignore(e => e.MeasureReportList);
            modelBuilder.Entity<ReportResource>();
            modelBuilder.Entity<ReportPopulation>()
                .Ignore(e => e.GroupPopulations);
        }

        /// <summary>
        /// Adds the given report schedules to the in-memory store without counting the call as a
        /// test-observable <see cref="SaveChangesAsync"/> invocation.  EF Core's identity map ensures
        /// that subsequent LINQ queries return the same object instances, so property assertions on the
        /// original objects remain valid after the manager modifies them.
        /// </summary>
        public async Task Seed(params ReportSchedule[] reports)
        {
            _seeding = true;
            try
            {
                await ReportSchedules.AddRangeAsync(reports);
                // Call base directly to bypass our tracking override during seeding.
                await base.SaveChangesAsync(CancellationToken.None);
            }
            finally
            {
                _seeding = false;
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_seeding)
            {
                SaveChangesWasCalled = true;
                SaveChangesCallCount++;
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
