using LantanaGroup.Link.Report.Controllers;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

[Trait("Category", "UnitTests")]
public class ReportScheduleControllerSoftDeleteTests
{
    [Fact]
    public async Task SoftDelete_PassesAllowInProgressToManager()
    {
        var id = Guid.NewGuid();
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.SoftDeleteByReportTrackingIdAsync(id, It.IsAny<CancellationToken>(), true))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var result = await CreateController(manager).SoftDelete(id.ToString(), allowInProgress: true);

        Assert.IsType<NoContentResult>(result);
        manager.Verify();
    }

    [Fact]
    public async Task SoftDelete_Default_DoesNotAllowInProgress()
    {
        var id = Guid.NewGuid();
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.SoftDeleteByReportTrackingIdAsync(id, It.IsAny<CancellationToken>(), false))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var result = await CreateController(manager).SoftDelete(id.ToString());

        Assert.IsType<NoContentResult>(result);
        manager.Verify();
    }

    [Fact]
    public async Task SoftDelete_InProgressWithoutBypass_Returns409Problem()
    {
        var id = Guid.NewGuid();
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(m => m.SoftDeleteByReportTrackingIdAsync(id, It.IsAny<CancellationToken>(), false))
            .ThrowsAsync(new InvalidOperationException($"Report schedule '{id}' is currently in progress and cannot be deleted."));

        var result = await CreateController(manager).SoftDelete(id.ToString());

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("currently in progress", problem.Detail);
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
