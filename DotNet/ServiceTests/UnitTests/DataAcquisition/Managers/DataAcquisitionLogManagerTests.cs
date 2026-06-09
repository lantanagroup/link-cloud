using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Medallion.Threading;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Managers;

[Trait("Category", "UnitTests")]
public class DataAcquisitionLogManagerTests
{
    private readonly Mock<ILogger<DataAcquisitionLogManager>> _logger = new();
    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<IDataAcquisitionLogQueries> _queries = new();
    private readonly Mock<IEntityRepository<DataAcquisitionLog>> _logRepo = new();
    private readonly Mock<IDistributedSemaphoreProvider> _semaphoreProvider = new();
    private readonly Mock<IResourceCache> _resourceCache = new();
    private readonly DataAcquisitionDbContext _dbContext;

    public DataAcquisitionLogManagerTests()
    {
        _database.SetupGet(d => d.DataAcquisitionLogRepository).Returns(_logRepo.Object);

        // A real (but never persisted) DbContext satisfies the constructor;
        // tests in this class only exercise paths that don't touch it.
        var options = new DbContextOptionsBuilder<DataAcquisitionDbContext>()
            .UseInMemoryDatabase($"DataAcquisitionLogManagerTests_{Guid.NewGuid():N}")
            .Options;
        _dbContext = new DataAcquisitionDbContext(options);

        var semaphoreHandle = new Mock<IDistributedSynchronizationHandle>();
        var semaphore = new Mock<IDistributedSemaphore>();
        semaphore
            .Setup(s => s.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IDistributedSynchronizationHandle?>(semaphoreHandle.Object));
        _semaphoreProvider
            .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphore.Object);
    }

    private DataAcquisitionLogManager CreateManager() =>
        new(_logger.Object, _database.Object, _dbContext, _queries.Object, _semaphoreProvider.Object, _resourceCache.Object);

    [Fact]
    public async Task CreateAsync_NullModel_ThrowsArgumentNull()
    {
        var manager = CreateManager();

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(null!));
    }

    [Fact]
    public async Task CreateAsync_NullFacilityId_ThrowsArgumentNull()
    {
        var manager = CreateManager();
        var model = new CreateDataAcquisitionLogModel
        {
            FacilityId = null!,
            QueryType = FhirQueryType.Read,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial
        };

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task CreateAsync_InvalidReportTrackingId_ThrowsArgumentException()
    {
        var manager = CreateManager();
        var model = new CreateDataAcquisitionLogModel
        {
            FacilityId = "TestFacility",
            QueryType = FhirQueryType.Read,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial,
            ReportTrackingId = "not-a-guid"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => manager.CreateAsync(model));
    }

    [Fact]
    public async Task DeleteAsync_NoExistingId_ThrowsNotFound()
    {
        _logRepo.Setup(r => r.GetAsync(It.IsAny<object>())).ReturnsAsync((DataAcquisitionLog)null!);
        var manager = CreateManager();

        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync(999));
    }

    [Fact]
    public async Task DeleteAsync_DefaultId_ThrowsInvalidOperation()
    {
        var manager = CreateManager();

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.DeleteAsync(0));
    }

    [Fact]
    public async Task SoftDeleteByFacilityAsync_NullFacilityId_ThrowsArgumentNull()
    {
        var manager = CreateManager();

        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.SoftDeleteByFacilityAsync(null!));
    }

    [Fact]
    public async Task UpdateTailFlag_InvalidReportTrackingId_ThrowsArgumentException()
    {
        var manager = CreateManager();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            manager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(
                new List<long> { 1 }, "TestFacility", "TestCorr", "not-a-guid"));
    }
}
