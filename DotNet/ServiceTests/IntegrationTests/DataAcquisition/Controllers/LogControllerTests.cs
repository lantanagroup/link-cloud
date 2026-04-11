using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Controllers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class LogControllerTests
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
        var logNotesQueries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogNotesQueries>();
        var referenceResourcesQueries = scope.ServiceProvider.GetRequiredService<IReferenceResourcesQueries>();
        return new LogController(logger, logService, logManager, logQueries, logNotesQueries, referenceResourcesQueries);
    }

    [Fact]
    public async Task Search_ValidParameters_ReturnsOkWithPagedResults()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{tag}";
        var reportTrackingId = $"TestReportId_{tag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        // Seed a log
        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
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
            FacilityId = facilityId,
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
        Assert.Equal(log.Id, pagedModel.Records[0].Id);
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var controller = CreateController(scope);

        var maxExistingId = await dbContext.DataAcquisitionLogs
            .OrderByDescending(l => l.Id)
            .Select(l => l.Id)
            .FirstOrDefaultAsync();
        var nonExistingId = maxExistingId + 10_000;

        // Act
        var result = await controller.Process(nonExistingId);

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

    // ==================== CancelBulk Tests ====================

    [Fact]
    public async Task CancelBulk_NullIds_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBulk_EmptyIds_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long>());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBulk_NegativeMinAgeHours_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long> { 1 }, minAgeHours: -1);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBulk_EligibleLogs_CancelsAndReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48) // old enough to be eligible
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long> { log.Id }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task CancelBulk_LogInTerminalStatus_DoesNotCancel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Completed,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long> { log.Id }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        var ineligible = (int)value.GetType().GetProperty("ineligible")!.GetValue(value)!;
        Assert.Equal(0, cancelled);
        Assert.Equal(1, ineligible);
    }

    [Fact]
    public async Task CancelBulk_LogTooRecent_DoesNotCancel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-1) // only 1 hour old
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long> { log.Id }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelBulk_MixedEligibility_CancelsOnlyEligible()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var eligibleLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        var completedLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Completed,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        var recentLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-1)
        };
        dbContext.DataAcquisitionLogs.AddRange(eligibleLog, completedLog, recentLog);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(
            new List<long> { eligibleLog.Id, completedLog.Id, recentLog.Id }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        var ineligible = (int)value.GetType().GetProperty("ineligible")!.GetValue(value)!;
        Assert.Equal(3, requested);
        Assert.Equal(1, cancelled);
        Assert.Equal(2, ineligible);
    }

    [Fact]
    public async Task CancelBulk_NonExistingIds_ReturnsZeroCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long> { 9999, 8888 }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelBulk_MinAgeZero_CancelsRecentLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow // just created
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelBulk(new List<long> { log.Id }, minAgeHours: 0);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, cancelled);
    }

    // ==================== CancelByFilter Tests ====================

    [Fact]
    public async Task CancelByFilter_NullParameters_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelByFilter_NoFilterCriteria_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(new LogSearchParameters());

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("At least one filter criteria must be provided.", badRequestResult.Value);
    }

    [Fact]
    public async Task CancelByFilter_ByFacilityId_CancelsEligibleLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var targetLog = new DataAcquisitionLog
        {
            FacilityId = "FacilityA",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        var otherLog = new DataAcquisitionLog
        {
            FacilityId = "FacilityB",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        dbContext.DataAcquisitionLogs.AddRange(targetLog, otherLog);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(
            new LogSearchParameters { FacilityId = "FacilityA" }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, cancelled);

        // Verify the other facility's log was not cancelled
        await dbContext.Entry(otherLog).ReloadAsync();
        Assert.Equal(RequestStatus.Pending, otherLog.Status);
    }

    [Fact]
    public async Task CancelByFilter_ByPatientId_CancelsMatchingLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log1 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            PatientId = "Patient123",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        var log2 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            PatientId = "Patient456",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        dbContext.DataAcquisitionLogs.AddRange(log1, log2);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(
            new LogSearchParameters { PatientId = "Patient123" }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task CancelByFilter_NoMatchingLogs_ReturnsZeroCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(
            new LogSearchParameters { FacilityId = "NonExistentFacility" }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(0, requested);
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelByFilter_AllTerminalStatuses_ReturnsZeroCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Cancelled, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Skipped, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.MaxRetriesReached, CreateDate = DateTime.UtcNow.AddHours(-48) }
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(
            new LogSearchParameters { FacilityId = "TestFacility" }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(4, requested);
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelByFilter_ReturnsCorrectRequestedVsCancelledCounts()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // 2 eligible, 1 completed, 1 too recent = 4 requested, 2 cancelled, 2 ineligible
        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Failed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "TestFacility", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-1) }
        );
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(
            new LogSearchParameters { FacilityId = "TestFacility" }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        var ineligible = (int)value.GetType().GetProperty("ineligible")!.GetValue(value)!;
        Assert.Equal(4, requested);
        Assert.Equal(2, cancelled);
        Assert.Equal(2, ineligible);
    }

    [Fact]
    public async Task CancelByFilter_ExcludesDeletedLogs_ByDefault()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var deletedLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48),
            IsDeleted = true
        };
        var activeLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48),
            IsDeleted = false
        };
        dbContext.DataAcquisitionLogs.AddRange(deletedLog, activeLog);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);

        // Act
        var result = await controller.CancelByFilter(
            new LogSearchParameters { FacilityId = "TestFacility", IncludeDeleted = false }, minAgeHours: 24);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var value = acceptedResult.Value;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    // ==================== ProcessByFilter Tests ====================

    [Fact]
    public async Task ProcessByFilter_NoFilter_ReturnsBadRequest()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var controller = CreateController(scope);
        var queryParams = new LogSearchParameters();

        // Act
        var result = await controller.ProcessByFilter(queryParams);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("At least one filter criteria must be provided.", badRequestResult.Value);
    }

    [Fact]
    public async Task ProcessByFilter_WithFilter_ReturnsAccepted()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(scope);
        var queryParams = new LogSearchParameters
        {
            FacilityId = "TestFacility"
        };

        // Act
        var result = await controller.ProcessByFilter(queryParams);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }
}

