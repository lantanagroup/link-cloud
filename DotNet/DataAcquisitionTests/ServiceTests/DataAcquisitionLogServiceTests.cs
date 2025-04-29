using LantanaGroup.Link.DataAcquisition.Application.Services;
using Moq;
using LantanaGroup.Link.Shared.Application.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LantanaGroup.Link.DataAcquisition.Application.Managers;
using Microsoft.Extensions.Logging;
using DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace DataAcquisitionTests.ServiceTests;
public class DataAcquisitionLogServiceTests
{
    private readonly Mock<IDataAcquisitionLogManager> _mockLogManager;
    private readonly Mock<ILogger<DataAcquisitionLogService>> _mockLogger;
    private readonly DataAcquisitionLogService _service;

    public DataAcquisitionLogServiceTests()
    {
        _mockLogManager = new Mock<IDataAcquisitionLogManager>();
        _mockLogger = new Mock<ILogger<DataAcquisitionLogService>>();
        _service = new DataAcquisitionLogService(_mockLogger.Object, _mockLogManager.Object);
    }

    [Fact]
    public async Task GetLogEntryById_ShouldReturnLogEntry_WhenLogExists()
    {
        // Arrange
        var logId = "123";
        var domainLog = new DataAcquisitionLog { Id = logId, Notes = new List<string> { "Test Log" } };
        _mockLogManager
            .Setup(manager => manager.GetAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainLog);

        // Act
        var result = await _service.GetLogEntryById(logId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(logId, result.Id);
        Assert.Equal("Test Log", result.Notes[0]);
        _mockLogManager.Verify(manager => manager.GetAsync(logId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLogEntryById_ShouldThrowException_WhenLogDoesNotExist()
    {
        // Arrange
        var logId = "123";
        _mockLogManager
            .Setup(manager => manager.GetAsync(logId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Log not found"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _service.GetLogEntryById(logId));
        _mockLogManager.Verify(manager => manager.GetAsync(logId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetQueryLogSummariesForFacility_ShouldReturnSummaries_WhenLogsExist()
    {
        // Arrange
        var facilityId = "Facility1";
        var page = 1;
        var pageSize = 10;
        var sortBy = "Date";
        var sortOrder = SortOrder.Ascending;

        var domainLogs = new List<DataAcquisitionLog>
            {
                new DataAcquisitionLog { Id = "1", Notes = new List<string> { "Test Log 1" } },
                new DataAcquisitionLog { Id = "2", Notes = new List<string> { "Test Log 2" } }
            };
        var paginationMetadata = new PaginationMetadata { TotalCount = 2, PageSize = 10, PageNumber = 1, TotalPages = 1 };

        _mockLogManager
            .Setup(manager => manager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, It.IsAny<CancellationToken>()))
            .ReturnsAsync((domainLogs, paginationMetadata));

        // Act
        var result = await _service.GetQueryLogSummariesForFacility(facilityId, page, pageSize, sortBy, sortOrder);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal("1", result.Records[0].Id);
        Assert.Equal("2", result.Records[1].Id);
        Assert.Equal(2, result.Metadata.TotalCount);
        _mockLogManager.Verify(manager => manager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetQueryLogSummariesForFacility_ShouldReturnEmpty_WhenNoLogsExist()
    {
        // Arrange
        var facilityId = "Facility1";
        var page = 1;
        var pageSize = 10;
        var sortBy = "Date";
        var sortOrder = SortOrder.Ascending;

        _mockLogManager
            .Setup(manager => manager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<DataAcquisitionLog>(), new PaginationMetadata { TotalCount = 0, PageSize = 10, PageNumber = 1, TotalPages = 1 }));

        // Act
        var result = await _service.GetQueryLogSummariesForFacility(facilityId, page, pageSize, sortBy, sortOrder);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Records);
        Assert.Equal(0, result.Metadata.TotalCount);
        _mockLogManager.Verify(manager => manager.GetByFacilityIdAsync(facilityId, page, pageSize, sortBy, sortOrder, It.IsAny<CancellationToken>()), Times.Once);
    }
}
