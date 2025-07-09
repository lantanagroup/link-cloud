using Moq;
using LantanaGroup.Link.Shared.Application.Enums;
using Microsoft.Extensions.Logging;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using System.Linq.Expressions;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Xunit;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;

namespace LantanaGroup.Link.DataAcquisitionTests.ServiceTests;
public class DataAcquisitionLogTests
{
    private readonly Mock<IDataAcquisitionLogManager> _mockLogManager;
    private readonly Mock<IDataAcquisitionLogQueries> _mockLogQueries;
    private readonly Mock<ILogger<DataAcquisitionLogManager>> _mockManagerLogger;
    private readonly DataAcquisitionLogService _service;
    private readonly Mock<IProducer<string, ResourceAcquired>> _mockProducer;
    private readonly Mock<IDatabase> _mockDatabase;

    public DataAcquisitionLogTests()
    {
        _mockLogManager = new Mock<IDataAcquisitionLogManager>();
        _mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
        _mockManagerLogger = new Mock<ILogger<DataAcquisitionLogManager>>();
        _mockProducer = new Mock<IProducer<string, ResourceAcquired>>();
        _mockDatabase = new Mock<IDatabase>();
    }

    [Fact]
    public async Task DataAcquisitionLogManager_GetAsync_DataAcquisitionLog()
    {
        // Arrange
        var logId = "123";
        var log = new DataAcquisitionLog { Id = logId };

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(logId))
            .ReturnsAsync(log); // Mock the repository method

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.GetAsync(logId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logId, result.Id);

        // Verify that the repository's GetAsync method was called with the correct parameters
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.GetAsync(logId), Times.Once);
    }

    [Fact]
    public async Task GetLogEntryById_ShouldThrowException_WhenLogDoesNotExist()
    {
        // Arrange
        var logId = "123";

        // Mock the repository to return null when GetAsync is called with the specified logId
        _mockLogQueries
            .Setup(m => m.GetDataAcquisitionLogAsync(logId, CancellationToken.None))
            .ReturnsAsync((DataAcquisitionLog?)null);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object,_mockLogQueries.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => manager.GetModelAsync(logId));
        Assert.Equal($"No log found for id: {logId}", exception.Message);

        // Verify that the GetAsync method was called exactly once with the correct logId
        _mockLogQueries.Verify(m => m.GetDataAcquisitionLogAsync(logId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateLog()
    {
        // Arrange
        var log = new DataAcquisitionLog
        {
            FacilityId = "Facility1",
            FhirQuery = new List<FhirQuery> { new FhirQuery { ResourceReferenceTypes = new List<ResourceReferenceType>() } }
        };

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.AddAsync(It.IsAny<DataAcquisitionLog>()))
            .ReturnsAsync((DataAcquisitionLog log) => log); // Return the same log object

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.CreateAsync(log);

        // Assert
        Assert.NotNull(result.Id);
        Assert.NotNull(result.CreateDate);
        Assert.NotNull(result.ModifyDate);
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.AddAsync(It.IsAny<DataAcquisitionLog>()), Times.Once);
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.SaveChangesAsync(), Times.Once);
    }


    //------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_ShouldDeleteLog()
    {
        // Arrange
        var logId = "123";
        var log = new DataAcquisitionLog { Id = logId };

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(logId))
            .ReturnsAsync(log);

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.Remove(It.IsAny<DataAcquisitionLog>()))
            .Verifiable();

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        await manager.DeleteAsync(logId);

        // Assert
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.Remove(It.IsAny<DataAcquisitionLog>()), Times.Once);
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetModelAsync_ShouldReturnLogModel()
    {
        // Arrange
        var logId = "123";
        var log = new DataAcquisitionLog
        {
            Id = logId,
            FacilityId = "Facility1",
            PatientId = "Patient1",
            Status = RequestStatus.Completed,
            ExecutionDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "Report1", // Correct property
                ReportTypes = new List<string> { "Type1", "Type2" }, // Example data
                Frequency = Frequency.Daily, // Example frequency
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(7)
            }
        };
        
        _mockLogQueries
            .Setup(m => m.GetDataAcquisitionLogAsync(logId, CancellationToken.None))
            .ReturnsAsync(log);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.GetModelAsync(logId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logId, result.Id);
        Assert.Equal(log.FacilityId, result.FacilityId);
        Assert.Equal(log.PatientId, result.PatientId);
        Assert.Equal(log.ExecutionDate, result.ExecutionDate);
        Assert.NotNull(result.ScheduledReport);
        Assert.Equal(log.ScheduledReport.ReportTrackingId, result.ScheduledReport.ReportTrackingId); // Correct property
        Assert.Equal(log.ScheduledReport.ReportTypes, result.ScheduledReport.ReportTypes);
    }

    private RequestStatusModel? MapRequestStatus(RequestStatus? status)
    {
        if (status == null)
            return null;

        return status switch
        {
            RequestStatus.Pending => RequestStatusModel.Pending,
            RequestStatus.Completed => RequestStatusModel.Completed,
            RequestStatus.Failed => RequestStatusModel.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status), $"Unhandled status: {status}")
        };
    }


    [Fact]
    public async Task UpdateAsync_ShouldUpdateLog()
    {
        // Arrange
        var logId = "123";
        var log = new DataAcquisitionLog { Id = logId, Status = RequestStatus.Pending };

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(logId))
            .ReturnsAsync(log);

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.SaveChangesAsync())
            .Returns(Task.CompletedTask);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.UpdateAsync(log);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logId, result.Id);
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByFacilityIdAsync_ShouldReturnPagedLogs()
    {
        // Arrange
        var facilityId = "Facility1";
        var logs = new List<DataAcquisitionLog> { new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, FhirQuery = new List<FhirQuery> { new FhirQuery { FacilityId = facilityId, QueryType = FhirQueryType.Read, ResourceTypes = new List<Hl7.Fhir.Model.ResourceType> { Hl7.Fhir.Model.ResourceType.Patient } } } } };
        
            var metadata = new PaginationMetadata { TotalCount = 1 };

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.SearchAsync(
                It.IsAny<Expression<Func<DataAcquisitionLog, bool>>>(),
                It.IsAny<string>(),
                It.IsAny<SortOrder>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((logs, metadata));

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.GetByFacilityIdAsync(facilityId, 1, 10, "Id", SortOrder.Ascending);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Records);
        Assert.Equal(facilityId, result.Records.First().FacilityId);
    }


    [Fact]
    public async Task SearchAsync_ShouldReturnPagedLogs()
    {
        // Arrange
        var request = new SearchDataAcquisitionLogRequest
        {
            FacilityId = "Facility1",
            PatientId = "Patient1",
            PageNumber = 1,
            PageSize = 10
        };

        var logs = new List<DataAcquisitionLog>
        {
            new DataAcquisitionLog 
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = request.FacilityId, 
                PatientId = request.PatientId,
                FhirQuery = new List<FhirQuery>
                {
                    new FhirQuery
                    {
                        Id = Guid.NewGuid().ToString(),
                        FacilityId = request.FacilityId,
                        ResourceTypes = new List<ResourceType> { ResourceType.Patient },
                    }
                },
                FhirVersion = "R4",
                QueryType = FhirQueryType.Read,
                QueryPhase = QueryPhase.Initial,
                ExecutionDate = DateTime.Now,
                Status = DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus.Completed,
            }
        };
        var metadata = new PaginationMetadata { TotalCount = 1 };
        
        var summaryLogs = logs.Select(QueryLogSummaryModel.FromDomain).ToList();

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.SearchAsync(
                It.IsAny<Expression<Func<DataAcquisitionLog, bool>>>(),
                It.IsAny<string>(),
                It.IsAny<SortOrder>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync((logs, metadata));

        _mockLogQueries
            .Setup(m => m.SearchAsync(
                It.IsAny<SearchDataAcquisitionLogRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((summaryLogs, 1));
        
        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.SearchAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Records);
    }

    [Fact]
    public async Task GetPendingRequests_ShouldReturnPendingLogs()
    {
        // Arrange
        var logs = new List<DataAcquisitionLog>
    {
        new DataAcquisitionLog { Status = RequestStatus.Pending, ExecutionDate = DateTime.UtcNow.AddHours(1) }
    };

        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.FindAsync(It.IsAny<Expression<Func<DataAcquisitionLog, bool>>>()))
            .ReturnsAsync(logs);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.GetPendingRequests();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(RequestStatus.Pending, result.First().Status);
    }

    //________________________________________________________________________________________

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenLogIsNull()
    {
        // Arrange
        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(null));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowException_WhenIdIsNullOrEmpty()
    {
        // Arrange
        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.DeleteAsync(null));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.DeleteAsync(string.Empty));
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowNotFoundException_WhenLogDoesNotExist()
    {
        // Arrange
        var logId = "123";
        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(logId))
            .ReturnsAsync((DataAcquisitionLog)null);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync(logId));
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenLogDoesNotExist()
    {
        // Arrange
        var logId = "123";
        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(logId))
            .ReturnsAsync((DataAcquisitionLog)null);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.GetAsync(logId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetModelAsync_ShouldThrowNotFoundException_WhenLogDoesNotExist()
    {
        // Arrange
        var logId = "123";
        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(logId))
            .ReturnsAsync((DataAcquisitionLog)null);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.GetModelAsync(logId));
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowException_WhenLogIsNull()
    {
        // Arrange
        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.UpdateAsync((DataAcquisitionLog)null));
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowNotFoundException_WhenLogDoesNotExist()
    {
        // Arrange
        var log = new DataAcquisitionLog { Id = "123" };
        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.GetAsync(log.Id))
            .ReturnsAsync((DataAcquisitionLog)null);

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<DataAcquisitionLogNotFoundException>(() => manager.UpdateAsync(log));
    }

    [Fact]
    public async Task SearchAsync_ShouldThrowException_WhenRequestIsNull()
    {
        // Arrange
        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.SearchAsync(null));
    }

    [Fact]
    public async Task GetPendingRequests_ShouldReturnEmptyList_WhenNoPendingLogsExist()
    {
        // Arrange
        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.FindAsync(It.IsAny<Expression<Func<DataAcquisitionLog, bool>>>()))
            .ReturnsAsync(new List<DataAcquisitionLog>());

        var manager = new DataAcquisitionLogManager(_mockManagerLogger.Object, _mockDatabase.Object, _mockLogQueries.Object);

        // Act
        var result = await manager.GetPendingRequests();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId_ShouldDoNothing_WhenNoMatchingLogsExist()
    {
        // Arrange
        var facilityId = "TestFacility";
        var correlationId = "TestCorrelation";
        var reportTrackingId = "TestReportTracking";
        var logIds = new List<string> { "test-id-1" }; // Add an empty list or appropriate log IDs

        // Mock the FindAsync method to return an empty list (no matching logs)
        _mockDatabase
            .Setup(m => m.DataAcquisitionLogRepository.FindAsync(It.IsAny<Expression<Func<DataAcquisitionLog, bool>>>()))
            .ReturnsAsync(new List<DataAcquisitionLog>());

        // Act
        await _mockLogManager.Object.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(logIds, facilityId, correlationId, reportTrackingId);

        // Assert
        _mockDatabase.Verify(m => m.DataAcquisitionLogRepository.SaveChangesAsync(), Times.Never); // Ensure SaveChangesAsync is not called
    }

}
