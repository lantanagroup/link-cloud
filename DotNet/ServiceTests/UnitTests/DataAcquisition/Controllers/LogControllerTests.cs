using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Moq.AutoMock;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Controllers;

[Trait("Category", "UnitTests")]
public class LogControllerTests
{
    private static LogController CreateController(AutoMocker mocker) =>
        mocker.CreateInstance<LogController>();

    // ==================== Search ====================

    [Fact]
    public async Task Search_NullParameters_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.Search(null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Search_InvalidSortBy_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.Search(new LogSearchParameters { SortBy = "InvalidField" });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Search_ValidParameters_ReturnsOkWithPagedResults()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogQueries>()
            .Setup(q => q.SearchQueryLogSummaryAsync(It.IsAny<SearchDataAcquisitionLogRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryLogSummaryModelResponse());

        var controller = CreateController(mocker);

        var result = await controller.Search(new LogSearchParameters
        {
            FacilityId = "Facility1",
            PageNumber = 1,
            PageSize = 10,
            SortBy = "FacilityId",
            SortOrder = SortOrder.Ascending
        });

        Assert.IsType<OkObjectResult>(result.Result);
    }

    // ==================== GetLogEntryById ====================

    [Fact]
    public async Task GetLogEntryById_DefaultId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.GetLogEntryById(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetLogEntryById_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogQueries>()
            .Setup(q => q.GetAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataAcquisitionLogModel?)null);

        var controller = CreateController(mocker);

        var result = await controller.GetLogEntryById(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetLogEntryById_ExistingId_ReturnsOkWithLog()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogQueries>()
            .Setup(q => q.GetAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataAcquisitionLogModel { Id = 5, FacilityId = "Facility1" });

        var controller = CreateController(mocker);

        var result = await controller.GetLogEntryById(5, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsAssignableFrom<DataAcquisitionLogModel>(okResult.Value);
    }

    // ==================== UpdateLogEntry ====================

    [Fact]
    public async Task UpdateLogEntry_InvalidId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.UpdateLogEntry(string.Empty, new UpdateDataAcquisitionLogModel { Id = 0 }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateLogEntry_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DataAcquisitionLogNotFoundException("Data acquisition log with ID 999 not found."));

        var controller = CreateController(mocker);

        var result = await controller.UpdateLogEntry("999", new UpdateDataAcquisitionLogModel { Id = 999 }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    // ==================== DeleteLogEntry ====================

    [Fact]
    public async Task DeleteLogEntry_InvalidId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.DeleteLogEntry(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteLogEntry_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("No log found for id: 999"));

        var controller = CreateController(mocker);

        var result = await controller.DeleteLogEntry(999, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteLogEntry_ExistingId_ReturnsNoContent()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(mocker);

        var result = await controller.DeleteLogEntry(5, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    // ==================== Process ====================

    [Fact]
    public async Task Process_InvalidId_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.Process(0);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Process_NonExistingId_ReturnsNotFound()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogService>()
            .Setup(s => s.StartRetrievalProcess(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DataAcquisitionLogNotFoundException("Not found"));

        var controller = CreateController(mocker);

        var result = await controller.Process(999);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task Process_ExistingId_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogService>()
            .Setup(s => s.StartRetrievalProcess(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = CreateController(mocker);

        var result = await controller.Process(5);

        Assert.IsType<AcceptedResult>(result);
    }

    // ==================== CancelBulk ====================

    [Fact]
    public async Task CancelBulk_NullIds_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.CancelBulk(null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBulk_EmptyIds_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.CancelBulk(new List<long>());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBulk_NegativeMinAgeHours_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.CancelBulk(new List<long> { 1 }, minAgeHours: -1);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelBulk_AllEligible_ReturnsAcceptedWithCount()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.CancelBulkAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var controller = CreateController(mocker);

        var result = await controller.CancelBulk(new List<long> { 1 }, minAgeHours: 24);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var value = accepted.Value!;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task CancelBulk_PartialEligible_ReturnsAcceptedWithIneligibleCount()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.CancelBulkAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var controller = CreateController(mocker);

        var result = await controller.CancelBulk(new List<long> { 1, 2, 3 }, minAgeHours: 24);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var value = accepted.Value!;
        var ineligible = (int)value.GetType().GetProperty("ineligible")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(1, cancelled);
        Assert.Equal(2, ineligible);
    }

    [Fact]
    public async Task CancelBulk_NonExistingIds_ReturnsZeroCancelled()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.CancelBulkAsync(It.IsAny<IEnumerable<long>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var controller = CreateController(mocker);

        var result = await controller.CancelBulk(new List<long> { 9999, 8888 }, minAgeHours: 24);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var value = accepted.Value!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(0, cancelled);
    }

    // ==================== CancelByFilter ====================

    [Fact]
    public async Task CancelByFilter_NullParameters_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.CancelByFilter(null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CancelByFilter_NoFilterCriteria_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.CancelByFilter(new LogSearchParameters());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("At least one filter criteria must be provided.", badRequest.Value);
    }

    [Fact]
    public async Task CancelByFilter_WithFilter_ReturnsAcceptedWithCounts()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.CancelByFilterAsync(It.IsAny<SearchDataAcquisitionLogRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((requested: 4, cancelled: 2));

        var controller = CreateController(mocker);

        var result = await controller.CancelByFilter(new LogSearchParameters { FacilityId = "Facility1" }, minAgeHours: 24);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var value = accepted.Value!;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        var ineligible = (int)value.GetType().GetProperty("ineligible")!.GetValue(value)!;
        Assert.Equal(4, requested);
        Assert.Equal(2, cancelled);
        Assert.Equal(2, ineligible);
    }

    [Fact]
    public async Task CancelByFilter_NoMatchingLogs_ReturnsZeroCancelled()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogManager>()
            .Setup(m => m.CancelByFilterAsync(It.IsAny<SearchDataAcquisitionLogRequest>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((requested: 0, cancelled: 0));

        var controller = CreateController(mocker);

        var result = await controller.CancelByFilter(new LogSearchParameters { FacilityId = "NonExisting" }, minAgeHours: 24);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var value = accepted.Value!;
        var requested = (int)value.GetType().GetProperty("requested")!.GetValue(value)!;
        var cancelled = (int)value.GetType().GetProperty("cancelled")!.GetValue(value)!;
        Assert.Equal(0, requested);
        Assert.Equal(0, cancelled);
    }

    // ==================== ProcessByFilter ====================

    [Fact]
    public async Task ProcessByFilter_NoFilter_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.ProcessByFilter(new LogSearchParameters());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("At least one filter criteria must be provided.", badRequest.Value);
    }

    [Fact]
    public async Task ProcessByFilter_NullParameters_ReturnsBadRequest()
    {
        var mocker = new AutoMocker();
        var controller = CreateController(mocker);

        var result = await controller.ProcessByFilter(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ProcessByFilter_WithFilter_ReturnsAccepted()
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDataAcquisitionLogQueries>()
            .Setup(q => q.SearchAsync(It.IsAny<SearchDataAcquisitionLogRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<DataAcquisitionLogSummaryModel>(new List<DataAcquisitionLogSummaryModel>(), new PaginationMetadata(1, 10, 0)));

        var controller = CreateController(mocker);

        var result = await controller.ProcessByFilter(new LogSearchParameters { FacilityId = "Facility1" });

        Assert.IsType<AcceptedResult>(result);
    }
}
