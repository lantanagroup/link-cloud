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

    // A minimal testable subclass that bypasses the MongoDB-specific constructor logic
    // and exposes the list of items passed to UpdateRange for verification.
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

        SetupFindAsync(new List<ReportSchedule> { submittedReport });

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

        SetupFindAsync(new List<ReportSchedule> { submittedReport });

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

        SetupFindAsync(new List<ReportSchedule> { submittedReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithSubmittedReport_WhenDeleted_PassesReportToUpdateRange()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);

        SetupFindAsync(new List<ReportSchedule> { submittedReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.Contains(submittedReport, _context.UpdateRangeItems);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_CallsDeleteJob()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);
        scheduledReport.Id = "sched-report-id-1";

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
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_DoesNotSetIsDeleted()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled, isDeleted: false);

        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: Scheduled report should NOT have IsDeleted modified
        Assert.False(scheduledReport.IsDeleted);
        Assert.DoesNotContain(scheduledReport, _context.UpdateRangeItems);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithScheduledReport_WhenDeleted_DoesNotCallSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);

        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert: no Submitted reports, so SaveChanges should not be called
        Assert.False(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithEndOfPeriodReport_WhenDeleted_LeavesReportUntouched()
    {
        // Arrange
        var facilityId = "FAC001";
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: false);
        var originalModifyDate = endOfPeriodReport.ModifyDate;

        SetupFindAsync(new List<ReportSchedule> { endOfPeriodReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.False(endOfPeriodReport.IsDeleted);
        Assert.Equal(originalModifyDate, endOfPeriodReport.ModifyDate);
        Assert.DoesNotContain(endOfPeriodReport, _context.UpdateRangeItems);
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

        SetupFindAsync(new List<ReportSchedule> { newReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.False(newReport.IsDeleted);
        Assert.Equal(originalModifyDate, newReport.ModifyDate);
        Assert.DoesNotContain(newReport, _context.UpdateRangeItems);
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

        SetupFindAsync(new List<ReportSchedule> { submittedReport });

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

        SetupFindAsync(new List<ReportSchedule> { submittedReport });

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
    public async Task UpdateReportsDeletedStatusForFacility_WhenDeleteJobThrows_ExceptionIsCaughtAndDoesNotPropagate()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);

        SetupFindAsync(new List<ReportSchedule> { scheduledReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Quartz job store error"));

        // Act — should not throw
        var act = async () => await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        await act(); // no exception
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

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

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
    public async Task UpdateReportsDeletedStatusForFacility_WithOnlyScheduledReports_WhenDeleted_DoesNotCallSaveChangesAsync()
    {
        // Arrange
        var facilityId = "FAC001";
        var scheduledReport1 = CreateReport(facilityId, ScheduleStatus.Scheduled);
        var scheduledReport2 = CreateReport(facilityId, ScheduleStatus.Scheduled);

        SetupFindAsync(new List<ReportSchedule> { scheduledReport1, scheduledReport2 });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.False(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMultipleSubmittedReports_UpdatesAllOfThemAndSavesOnce()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport1 = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);
        var submittedReport2 = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);

        SetupFindAsync(new List<ReportSchedule> { submittedReport1, submittedReport2 });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.True(submittedReport1.IsDeleted);
        Assert.True(submittedReport2.IsDeleted);
        Assert.Contains(submittedReport1, _context.UpdateRangeItems);
        Assert.Contains(submittedReport2, _context.UpdateRangeItems);
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
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: false);
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);
        scheduledReport.Id = "sched-mixed-id";
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: false);
        var newReport = CreateReport(facilityId, ScheduleStatus.New, isDeleted: false);

        SetupFindAsync(new List<ReportSchedule> { submittedReport, scheduledReport, endOfPeriodReport, newReport });

        _mockQuartzJobHelper
            .Setup(q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: true);

        // Assert
        Assert.True(submittedReport.IsDeleted);
        Assert.Contains(submittedReport, _context.UpdateRangeItems);

        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob("sched-mixed-id", "MeasureReportSubmissionGroup", It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.False(endOfPeriodReport.IsDeleted);
        Assert.DoesNotContain(endOfPeriodReport, _context.UpdateRangeItems);

        Assert.False(newReport.IsDeleted);
        Assert.DoesNotContain(newReport, _context.UpdateRangeItems);

        Assert.True(_context.SaveChangesWasCalled);
    }

    [Fact]
    public async Task UpdateReportsDeletedStatusForFacility_WithMixedStatuses_WhenNotDeleted_OnlySubmittedAreUpdated()
    {
        // Arrange
        var facilityId = "FAC001";
        var submittedReport = CreateReport(facilityId, ScheduleStatus.Submitted, isDeleted: true);
        var scheduledReport = CreateReport(facilityId, ScheduleStatus.Scheduled);
        var endOfPeriodReport = CreateReport(facilityId, ScheduleStatus.EndOfPeriod, isDeleted: true);

        SetupFindAsync(new List<ReportSchedule> { submittedReport, scheduledReport, endOfPeriodReport });

        // Act
        await _sut.UpdateReportsDeletedStatusForFacility(facilityId, deleted: false);

        // Assert
        Assert.False(submittedReport.IsDeleted);

        _mockQuartzJobHelper.Verify(
            q => q.DeleteJob(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.DoesNotContain(endOfPeriodReport, _context.UpdateRangeItems);

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

    private void SetupFindAsync(List<ReportSchedule> reports)
    {
        _mockRepository
            .Setup(r => r.FindAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<ReportSchedule, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);
    }

    #endregion

    /// <summary>
    /// A minimal concrete subclass of <see cref="MongoDbContext"/> that bypasses the MongoDB-specific
    /// infrastructure and intercepts the <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
    /// and <see cref="DbSet{TEntity}.UpdateRange"/> calls for verification in unit tests.
    /// </summary>
    private sealed class TestableMongoDbContext : MongoDbContext
    {
        private readonly List<ReportSchedule> _updateRangeItems = new();

        public IReadOnlyList<ReportSchedule> UpdateRangeItems => _updateRangeItems;
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

        /// <summary>
        /// Intercepts <see cref="DbSet{TEntity}.UpdateRange"/> calls by recording the items
        /// rather than forwarding them to the EF Core change tracker (which would fail without a
        /// real MongoDB provider).
        /// </summary>
        public new void UpdateRange<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            if (entities is IEnumerable<ReportSchedule> schedules)
            {
                _updateRangeItems.AddRange(schedules);
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesWasCalled = true;
            SaveChangesCallCount++;
            return Task.FromResult(0);
        }
    }
}
