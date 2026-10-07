using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Infrastructure.Telemetry;
using LantanaGroup.Link.Audit.Presentation.Controllers;
using LantanaGroup.Link.DataAcquisition.Controllers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Audit;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class AcquisitionActivityCountEndpointTests
{
    [Fact]
    public async Task Rejects_days_outside_the_bound()
    {
        var queries = new Mock<IDataAcquisitionLogQueries>();
        var result = await Create(queries).Counts(new AcquisitionActivityCountRequest { Days = 32 }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        queries.Verify(item => item.GetActivityCountsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_the_aggregate()
    {
        var expected = new AcquisitionActivityCounts { FailedTotal = 6 };
        var queries = new Mock<IDataAcquisitionLogQueries>();
        queries
            .Setup(item => item.GetActivityCountsAsync(7, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Create(queries).Counts(new AcquisitionActivityCountRequest { Days = 7 }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    private static LogController Create(Mock<IDataAcquisitionLogQueries> queries) =>
        new(
            Mock.Of<ILogger<LogController>>(),
            Mock.Of<IDataAcquisitionLogService>(),
            Mock.Of<IDataAcquisitionLogManager>(),
            queries.Object,
            Mock.Of<IDataAcquisitionLogNotesQueries>(),
            Mock.Of<IReferenceResourcesQueries>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}

[Trait("Category", "UnitTests")]
public class AuditErrorCountEndpointTests
{
    [Fact]
    public async Task Rejects_hours_outside_the_bound()
    {
        var search = new Mock<ISearchRepository>();
        var result = await Create(search).CountErrors(0);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        search.Verify(item => item.CountErrorsAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_the_window_count()
    {
        var expected = new AuditErrorCount { Hours = 24, Errors = 3 };
        var search = new Mock<ISearchRepository>();
        search
            .Setup(item => item.CountErrorsAsync(24, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Create(search).CountErrors();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    private static AuditController Create(Mock<ISearchRepository> search) =>
        new(
            Mock.Of<ILogger<AuditController>>(),
            Mock.Of<IAuditServiceMetrics>(),
            search.Object,
            Mock.Of<IAuditRepository>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}
