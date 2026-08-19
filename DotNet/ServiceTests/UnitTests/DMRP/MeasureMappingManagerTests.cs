using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
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
    }
}
