using LantanaGroup.Link.Report.Controllers;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

[Trait("Category", "UnitTests")]
public class ReportScheduleControllerRestoreTests
{
    [Fact]
    public async Task Restore_WhenManagerSucceeds_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.RestoreByReportTrackingIdAsync(id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var result = await CreateController(manager).Restore(id.ToString());

        Assert.IsType<NoContentResult>(result);
        manager.Verify();
    }

    [Theory]
    [InlineData(ScheduleStatus.New)]
    [InlineData(ScheduleStatus.EndOfPeriod)]
    [InlineData(ScheduleStatus.CompletedNotSubmitted)]
    public async Task Restore_NonRestorableStatus_Returns409Problem(ScheduleStatus status)
    {
        var id = Guid.NewGuid();
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.RestoreByReportTrackingIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                $"{ScheduleStatusExtensions.RestoreBlockedDetail} Report schedule '{id}' is {status}."));

        var result = await CreateController(manager).Restore(id.ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains(ScheduleStatusExtensions.RestoreBlockedDetail, problem.Detail);
        Assert.Contains(status.ToString(), problem.Detail);
    }

    [Fact]
    public async Task Restore_MissingSchedule_Returns404()
    {
        var id = Guid.NewGuid();
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.RestoreByReportTrackingIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException($"Soft-deleted report schedule with ID '{id}' not found."));

        var result = await CreateController(manager).Restore(id.ToString());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static ReportScheduleController CreateController(Mock<IReportScheduledManager> manager) =>
        new(
            Mock.Of<ILogger<ReportScheduleController>>(),
            Mock.Of<IDatabase>(),
            manager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}
