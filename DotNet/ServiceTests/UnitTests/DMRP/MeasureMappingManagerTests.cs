using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    [Trait("Category", "UnitTests")]
    public class MeasureMappingManagerTests
    {
        private readonly Mock<ILogger<MeasureMappingManager>> _mockLogger;
        private readonly Mock<IEntityRepository<MeasureMapping>> _mockRepository;
        private readonly Mock<IEntityRepository<FacilityReportingPlan>> _mockReportingPlanRepository;
        private readonly MeasureMappingManager _manager;

        public MeasureMappingManagerTests()
        {
            _mockLogger = new Mock<ILogger<MeasureMappingManager>>();
            _mockRepository = new Mock<IEntityRepository<MeasureMapping>>();
            _mockReportingPlanRepository = new Mock<IEntityRepository<FacilityReportingPlan>>();
            _manager = new MeasureMappingManager(_mockLogger.Object, _mockRepository.Object,
                _mockReportingPlanRepository.Object);
        }

        [Fact]
        public async Task CreateAsync_SuccessfulCreation_CallsRepositoryAddAndSave()
        {
            var measureMapping = new MeasureMapping();

            _mockRepository.Setup(r => r.AddAsync(It.IsAny<MeasureMapping>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(measureMapping);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _manager.CreateAsync(measureMapping);

            Assert.Equal(measureMapping.Id, result.Id);
            _mockRepository.Verify(r => r.AddAsync(measureMapping, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_RepositoryThrows_ThrowsApplicationException()
        {
            _mockRepository.Setup(r => r.AddAsync(It.IsAny<MeasureMapping>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db failure"));

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(new MeasureMapping()));
        }

        [Fact]
        public async Task UpdateAsync_NotFound_ThrowsApplicationException()
        {
            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MeasureMapping?)null);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.UpdateAsync("missing-id", new MeasureMapping()));
        }

        [Fact]
        public async Task UpdateAsync_Found_CallsRepositoryUpdateAndSave()
        {
            var existing = new MeasureMapping();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _manager.UpdateAsync(existing.Id, new MeasureMapping { Id = existing.Id });

            _mockRepository.Verify(r => r.Update(existing), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_NotFound_ThrowsApplicationException()
        {
            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MeasureMapping?)null);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.DeleteAsync("missing-id"));
        }

        [Fact]
        public async Task DeleteAsync_Found_CallsRepositoryRemoveAndSave()
        {
            var existing = new MeasureMapping();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _manager.DeleteAsync(existing.Id);

            _mockRepository.Verify(r => r.Remove(existing), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_StillReferenced_RefusesBeforeTouchingTheRepository()
        {
            var existing = new MeasureMapping();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            ReportingPlansExist();

            await Assert.ThrowsAsync<MeasureMappingInUseException>(() => _manager.DeleteAsync(existing.Id));

            _mockRepository.Verify(r => r.Remove(It.IsAny<MeasureMapping>()), Times.Never);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAllAsync_OnlyUnmappedPlansExist_DeletesTheMappings()
        {
            // An enrollment Link has no mapping for holds no foreign key, so it is not what "in use"
            // means. A database of nothing but these must still be able to clear its mappings.
            ReportingPlans(new FacilityReportingPlan { MeasureMappingId = null });

            var mappings = new List<MeasureMapping> { new(), new() };

            _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mappings);

            await _manager.DeleteAllAsync();

            _mockRepository.Verify(r => r.Remove(It.IsAny<MeasureMapping>()), Times.Exactly(mappings.Count));
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAllAsync_AMappedPlanExists_RefusesBeforeTouchingTheRepository()
        {
            // The unmapped row alongside it must not be what carries the refusal - the mapped one must.
            ReportingPlans(
                new FacilityReportingPlan { MeasureMappingId = null },
                new FacilityReportingPlan { MeasureMappingId = "mapping-1" });

            await Assert.ThrowsAsync<MeasureMappingInUseException>(() => _manager.DeleteAllAsync());

            _mockRepository.Verify(r => r.Remove(It.IsAny<MeasureMapping>()), Times.Never);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        /// <summary>
        /// The pre-check above and the delete are two round trips, so a reporting plan can be created
        /// in between. These cover what the database says when that happens.
        /// </summary>
        /// <remarks>
        /// SQLite only. The SQL Server codes the same backstop recognises (547, 515) cannot be
        /// reached from a test: <see cref="Microsoft.Data.SqlClient.SqlException"/> has no public
        /// constructor and is only ever produced by the driver.
        /// </remarks>
        [Theory]
        [InlineData(787)]  // FOREIGN KEY - the dependent was untracked, so the DELETE reached the database.
        [InlineData(1299)] // NOT NULL - the dependent was tracked, so EF tried to null the foreign key first.
        public async Task DeleteAsync_SaveReportsTheRowIsStillReferenced_ThrowsMeasureMappingInUseException(
            int extendedErrorCode)
        {
            var existing = ArrangeDeleteFailure(new SqliteException("constraint failed", 19, extendedErrorCode));

            var exception = await Assert.ThrowsAsync<MeasureMappingInUseException>(
                () => _manager.DeleteAsync(existing.Id));

            Assert.Contains(existing.Id, exception.Message);
        }

        [Fact]
        public async Task DeleteAsync_SaveWrapsTheConstraintFailure_StillThrowsMeasureMappingInUseException()
        {
            // EF surfaces the provider's exception wrapped, at times more than one level deep, which is
            // why the backstop walks the chain rather than looking only at InnerException.
            var buried = new InvalidOperationException("outer",
                new DbUpdateException("An error occurred while saving the entity changes.",
                    new SqliteException("constraint failed", 19, 787)));

            var existing = ArrangeDeleteFailure(buried);

            await Assert.ThrowsAsync<MeasureMappingInUseException>(() => _manager.DeleteAsync(existing.Id));
        }

        [Fact]
        public async Task DeleteAsync_SaveFailsForAnotherReason_ThrowsApplicationException()
        {
            // SQLITE_BUSY. A failure that is not about the mapping being referenced must not be
            // reported as one, or the caller is told to delete reporting plans that are not the problem.
            var existing = ArrangeDeleteFailure(new SqliteException("database is locked", 5, 5));

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.DeleteAsync(existing.Id));
        }

        /// <summary>
        /// Sets up a mapping that passes the pre-check and then fails on save with
        /// <paramref name="saveFailure"/>, which is the race the backstop exists for.
        /// </summary>
        private MeasureMapping ArrangeDeleteFailure(Exception saveFailure)
        {
            var existing = new MeasureMapping();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(saveFailure);

            return existing;
        }

        private void ReportingPlansExist()
        {
            _mockReportingPlanRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
        }

        /// <summary>
        /// Answers AnyAsync by running the caller's predicate over <paramref name="plans"/> rather than
        /// returning a fixed answer, so a test can tell which question the guard asked and not merely
        /// that it asked one.
        /// </summary>
        private void ReportingPlans(params FacilityReportingPlan[] plans)
        {
            _mockReportingPlanRepository
                .Setup(r => r.AnyAsync(It.IsAny<Expression<Func<FacilityReportingPlan, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<FacilityReportingPlan, bool>> predicate, CancellationToken _) =>
                    plans.Any(predicate.Compile()));
        }
    }
}
