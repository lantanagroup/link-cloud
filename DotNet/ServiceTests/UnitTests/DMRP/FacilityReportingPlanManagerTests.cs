using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPlanManagerTests
    {
        private const string FacilityId = "100";

        private readonly Mock<ILogger<FacilityReportingPlanManager>> _mockLogger;
        private readonly Mock<IEntityRepository<FacilityReportingPlan>> _mockRepository;
        private readonly Mock<IEntityRepository<MeasureMapping>> _mockMeasureMappingRepository;
        private readonly Mock<IFacilityExistence> _mockFacilityExistence;
        private readonly FacilityReportingPlanManager _manager;

        public FacilityReportingPlanManagerTests()
        {
            _mockLogger = new Mock<ILogger<FacilityReportingPlanManager>>();
            _mockRepository = new Mock<IEntityRepository<FacilityReportingPlan>>();
            _mockMeasureMappingRepository = new Mock<IEntityRepository<MeasureMapping>>();
            _mockFacilityExistence = new Mock<IFacilityExistence>();

            // The happy path by default: the facility and the measure mapping both exist and the
            // period is not already taken. Each test spoils only what it is about.
            _mockFacilityExistence
                .Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockMeasureMappingRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<MeasureMapping, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _mockRepository
                .Setup(r => r.AddAsync(It.IsAny<FacilityReportingPlan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FacilityReportingPlan p, CancellationToken _) => p);

            _manager = new FacilityReportingPlanManager(_mockLogger.Object, _mockRepository.Object,
                _mockMeasureMappingRepository.Object, _mockFacilityExistence.Object);
        }

        private static FacilityReportingPlan ValidPlan() => new()
        {
            FacilityId = FacilityId,
            MeasureMappingId = Guid.NewGuid().ToString(),
            ReportingMonth = 5,
            ReportingYear = 2026,
            IsReporting = true
        };

        [Fact]
        public async Task CreateAsync_ValidPlan_PersistsTheRow()
        {
            var plan = ValidPlan();

            var result = await _manager.CreateAsync(plan);

            Assert.Equal(plan.Id, result.Id);
            _mockRepository.Verify(r => r.AddAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_MissingFacilityId_IsRejected()
        {
            var plan = ValidPlan();
            plan.FacilityId = "  ";

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(plan));
        }

        [Fact]
        public async Task CreateAsync_FacilityIdLongerThanTheColumn_IsRejected()
        {
            var plan = ValidPlan();
            plan.FacilityId = new string('9', 101);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(plan));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public async Task CreateAsync_MissingMeasureMappingId_IsRejected(string? measureMappingId)
        {
            var plan = ValidPlan();
            plan.MeasureMappingId = measureMappingId!;

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(plan));
            Assert.Equal("MeasureMappingId is required.", ex.Message);
        }

        [Fact]
        public async Task UpdateAsync_MissingMeasureMappingId_IsRejectedAndLeavesTheRowAlone()
        {
            var existing = ValidPlan();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var update = ValidPlan();
            update.MeasureMappingId = string.Empty;

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.UpdateAsync(existing.Id, update));

            Assert.NotEmpty(existing.MeasureMappingId);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(-1)]
        public async Task CreateAsync_MonthOutsideOneThroughTwelve_IsRejected(int month)
        {
            var plan = ValidPlan();
            plan.ReportingMonth = month;

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(plan));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1999)]
        [InlineData(2101)]
        public async Task CreateAsync_ImplausibleReportingYear_IsRejected(int year)
        {
            var plan = ValidPlan();
            plan.ReportingYear = year;

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(plan));
        }

        [Fact]
        public async Task CreateAsync_UnknownMeasureMapping_IsRejected()
        {
            _mockMeasureMappingRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<MeasureMapping, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(ValidPlan()));
        }

        [Fact]
        public async Task CreateAsync_FacilityNotInTenant_IsRejected()
        {
            _mockFacilityExistence
                .Setup(s => s.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(ValidPlan()));

            _mockFacilityExistence.Verify(
                s => s.ExistsAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_PeriodAlreadyRecordedForTheFacilityAndMapping_IsRejected()
        {
            _mockRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<DmrpConflictException>(() => _manager.CreateAsync(ValidPlan()));
        }

        [Fact]
        public async Task CreateAsync_InvalidPlan_IsNotWrittenToTheDatabase()
        {
            var plan = ValidPlan();
            plan.ReportingMonth = 13;

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(plan));

            _mockRepository.Verify(r => r.AddAsync(It.IsAny<FacilityReportingPlan>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_AnotherWriterTookThePeriodFirst_IsReportedAsAConflict()
        {
            // Passes the pre-check, then the unique index rejects the insert and the row is there.
            var duplicateExists = false;

            _mockRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => duplicateExists);

            _mockRepository
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => duplicateExists = true)
                .ThrowsAsync(new DbUpdateException("unique index violated"));

            await Assert.ThrowsAsync<DmrpConflictException>(() => _manager.CreateAsync(ValidPlan()));
        }

        [Fact]
        public async Task CreateAsync_DatabaseFailureThatIsNotADuplicate_IsNotDisguisedAsBadInput()
        {
            var failure = new DbUpdateException("the database is unavailable");

            _mockRepository
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            // Not an ApplicationException: the caller sent a valid plan, so this must surface as a
            // server error rather than a 400.
            var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => _manager.CreateAsync(ValidPlan()));

            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task UpdateAsync_DatabaseFailureThatIsNotADuplicate_IsNotDisguisedAsBadInput()
        {
            var failure = new DbUpdateException("the database is unavailable");

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(ValidPlan());
            _mockRepository
                .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(failure);

            var thrown = await Assert.ThrowsAsync<DbUpdateException>(
                () => _manager.UpdateAsync("some-id", ValidPlan()));

            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task UpdateAsync_UnknownId_ReportsNotFound()
        {
            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FacilityReportingPlan?)null);

            await Assert.ThrowsAsync<DmrpNotFoundException>(() => _manager.UpdateAsync("missing-id", ValidPlan()));
        }

        [Fact]
        public async Task UpdateAsync_AppliesEveryEditableField()
        {
            var existing = new FacilityReportingPlan
            {
                FacilityId = "old-facility",
                MeasureMappingId = "old-mapping",
                ReportingMonth = 1,
                ReportingYear = 2025,
                IsReporting = false
            };

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var update = ValidPlan();

            await _manager.UpdateAsync(existing.Id, update);

            Assert.Equal(update.FacilityId, existing.FacilityId);
            Assert.Equal(update.MeasureMappingId, existing.MeasureMappingId);
            Assert.Equal(update.ReportingMonth, existing.ReportingMonth);
            Assert.Equal(update.ReportingYear, existing.ReportingYear);
            Assert.Equal(update.IsReporting, existing.IsReporting);
            _mockRepository.Verify(r => r.Update(existing), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_InvalidPlan_IsRejectedRatherThanReportedMissing()
        {
            var existing = ValidPlan();
            existing.ReportingMonth = 3;

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            var update = ValidPlan();
            update.ReportingMonth = 13;

            // An ApplicationException, not a DmrpNotFoundException: the row exists, the request is bad.
            await Assert.ThrowsAsync<ApplicationException>(() => _manager.UpdateAsync(existing.Id, update));

            Assert.Equal(3, existing.ReportingMonth);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_UnknownId_ReportsNotFound()
        {
            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FacilityReportingPlan?)null);

            await Assert.ThrowsAsync<DmrpNotFoundException>(() => _manager.DeleteAsync("missing-id"));
        }

        [Fact]
        public async Task DeleteAsync_ExistingId_RemovesTheRow()
        {
            var existing = ValidPlan();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);

            await _manager.DeleteAsync(existing.Id);

            _mockRepository.Verify(r => r.Remove(existing), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAllAsync_RemovesEveryRowAndReportsHowMany()
        {
            var plans = new List<FacilityReportingPlan> { ValidPlan(), ValidPlan(), ValidPlan() };

            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(plans);

            var removed = await _manager.DeleteAllAsync();

            Assert.Equal(3, removed);
            foreach (var plan in plans)
            {
                _mockRepository.Verify(r => r.Remove(plan), Times.Once);
            }

            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAllAsync_EmptyTable_TouchesNothing()
        {
            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FacilityReportingPlan>());

            var removed = await _manager.DeleteAllAsync();

            Assert.Equal(0, removed);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteForFacilityAsync_RemovesThatFacilitysRowsAndReportsHowMany()
        {
            var plans = new List<FacilityReportingPlan> { ValidPlan(), ValidPlan() };

            _mockRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(plans);

            var removed = await _manager.DeleteForFacilityAsync(FacilityId);

            Assert.Equal(2, removed);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteForFacilityAsync_BlankFacilityId_IsRejected()
        {
            await Assert.ThrowsAsync<ApplicationException>(() => _manager.DeleteForFacilityAsync("  "));
        }
    }
}
