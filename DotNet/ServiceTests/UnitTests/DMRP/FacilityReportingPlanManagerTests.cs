using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DMRP
{
    [Trait("Category", "UnitTests")]
    public class FacilityReportingPlanManagerTests
    {
        private readonly Mock<ILogger<FacilityReportingPlanManager>> _mockLogger;
        private readonly Mock<IEntityRepository<FacilityReportingPlan>> _mockRepository;
        private readonly FacilityReportingPlanManager _manager;

        public FacilityReportingPlanManagerTests()
        {
            _mockLogger = new Mock<ILogger<FacilityReportingPlanManager>>();
            _mockRepository = new Mock<IEntityRepository<FacilityReportingPlan>>();
            _manager = new FacilityReportingPlanManager(_mockLogger.Object, _mockRepository.Object);
        }

        [Fact]
        public async Task CreateAsync_SuccessfulCreation_CallsRepositoryAddAndSave()
        {
            var plan = new FacilityReportingPlan();

            _mockRepository.Setup(r => r.AddAsync(It.IsAny<FacilityReportingPlan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(plan);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _manager.CreateAsync(plan);

            Assert.Equal(plan.Id, result.Id);
            _mockRepository.Verify(r => r.AddAsync(plan, It.IsAny<CancellationToken>()), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_RepositoryThrows_ThrowsApplicationException()
        {
            _mockRepository.Setup(r => r.AddAsync(It.IsAny<FacilityReportingPlan>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db failure"));

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.CreateAsync(new FacilityReportingPlan()));
        }

        [Fact]
        public async Task UpdateAsync_NotFound_ThrowsApplicationException()
        {
            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FacilityReportingPlan?)null);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.UpdateAsync("missing-id", new FacilityReportingPlan()));
        }

        [Fact]
        public async Task UpdateAsync_Found_CallsRepositoryUpdateAndSave()
        {
            var existing = new FacilityReportingPlan();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _manager.UpdateAsync(existing.Id, new FacilityReportingPlan { Id = existing.Id });

            _mockRepository.Verify(r => r.Update(existing), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_NotFound_ThrowsApplicationException()
        {
            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FacilityReportingPlan?)null);

            await Assert.ThrowsAsync<ApplicationException>(() => _manager.DeleteAsync("missing-id"));
        }

        [Fact]
        public async Task DeleteAsync_Found_CallsRepositoryRemoveAndSave()
        {
            var existing = new FacilityReportingPlan();

            _mockRepository.Setup(r => r.GetAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _mockRepository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            await _manager.DeleteAsync(existing.Id);

            _mockRepository.Verify(r => r.Remove(existing), Times.Once);
            _mockRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
