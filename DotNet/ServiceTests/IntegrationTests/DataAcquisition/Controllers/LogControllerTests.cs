using IntegrationTests.DataAcquisition;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Interfaces.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class LogControllerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public LogControllerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private LogController CreateController(IServiceScope scope)
    {
        var logger = new Mock<ILogger<LogController>>().Object;
        var logService = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogService>();
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();
        var logQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        return new LogController(logger, logService, logManager, logQueries);
    }

    [Fact]
    public async Task Search_ValidParameters_ReturnsOkWithPagedResults()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed a log
        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            QueryPhase = QueryPhase.Initial,
            Priority = AcquisitionPriority.Normal
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var queryParams = new LogSearchParameters
        {
            FacilityId = "TestFacility",
            PageNumber = 1,
            PageSize = 10,
            SortBy = "FacilityId",
            SortOrder = SortOrder.Ascending
        };

        // Act
        var result = await controller.Search(queryParams);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pagedModel = Assert.IsAssignableFrom<IPagedModel<QueryLogSummaryModel>>(okResult.Value);
        Assert.Single(pagedModel.Records);
    }

    [Fact]
    public async Task Search_InvalidSortBy_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var queryParams = new LogSearchParameters
        {
            SortBy = "InvalidField"
        };

        // Act
        var result = await controller.Search(queryParams);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Search_NullParameters_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.Search(null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLogById_ExistingId_ReturnsOkWithLog()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.GetLogEntryById(log.Id, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<DataAcquisitionLogModel>(okResult.Value);
    }

    [Fact]
    public async Task GetLogById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.GetLogEntryById(999, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<NotFoundResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task UpdateLogEntry_ExistingIdValidModel_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var updateModel = new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            Status = RequestStatus.Completed
        };

        // Act
        var result = await controller.UpdateLogEntry(log.Id.ToString(), updateModel, CancellationToken.None);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task UpdateLogEntry_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var updateModel = new UpdateDataAcquisitionLogModel
        {
            Id = 999,
            Status = RequestStatus.Completed
        };

        // Act
        var result = await controller.UpdateLogEntry("999", updateModel, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task UpdateLogEntry_InvalidId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var updateModel = new UpdateDataAcquisitionLogModel
        {
            Id = 0
        };

        // Act
        var result = await controller.UpdateLogEntry(string.Empty, updateModel, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteLogEntry_ExistingId_ReturnsNoContent()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility"
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteLogEntry(log.Id, CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task DeleteLogEntry_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteLogEntry(999, CancellationToken.None);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task DeleteLogEntry_InvalidId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.DeleteLogEntry(0, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Process_ExistingId_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.Process(log.Id);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public async Task Process_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.Process(999);

        // Assert
        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task Process_InvalidId_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.Process(0);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}