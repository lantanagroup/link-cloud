using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.Report.Controllers;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Tenant.Business.Managers;
using LantanaGroup.Link.Tenant.Business.Queries;
using LantanaGroup.Link.Tenant.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using static LantanaGroup.Link.Shared.Application.Extensions.Security.BackendAuthenticationServiceExtension;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

[Trait("Category", "UnitTests")]
public class ReportActivityCountEndpointTests
{
    [Fact]
    public async Task Rejects_days_outside_the_bound_and_both_facility_filters()
    {
        var manager = new Mock<IReportScheduledManager>();
        var controller = Create(manager);

        var low = await controller.Counts(new ReportActivityCountRequest { Days = 0 }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(low.Result);

        var both = await controller.Counts(new ReportActivityCountRequest
        {
            Days = 7,
            FacilityIds = ["a"],
            ExcludeFacilityIds = ["b"]
        }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(both.Result);

        var tooMany = await controller.Counts(new ReportActivityCountRequest
        {
            Days = 7,
            FacilityIds = Enumerable.Range(0, 5001).Select(index => $"id-{index}").ToList()
        }, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(tooMany.Result);

        manager.Verify(item => item.GetActivityCountsAsync(
            It.IsAny<int>(),
            It.IsAny<DateTime>(),
            It.IsAny<IReadOnlyCollection<string>?>(),
            It.IsAny<IReadOnlyCollection<string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accepts_one_facility_set_and_returns_the_aggregate()
    {
        var expected = new ReportActivityCounts { InFlight = 4, Submitted = 2 };
        var manager = new Mock<IReportScheduledManager>();
        manager
            .Setup(item => item.GetActivityCountsAsync(
                7,
                It.IsAny<DateTime>(),
                It.Is<IReadOnlyCollection<string>?>(ids => ids != null && ids.Contains("a")),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Create(manager).Counts(new ReportActivityCountRequest
        {
            Days = 7,
            FacilityIds = [" a ", "a"]
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }

    private static ReportScheduleController Create(Mock<IReportScheduledManager> manager) =>
        new(Mock.Of<ILogger<ReportScheduleController>>(), Mock.Of<IDatabase>(), manager.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}

[Trait("Category", "UnitTests")]
public class FacilityCountEndpointTests
{
    [Fact]
    public async Task Rejects_an_oversized_id_set()
    {
        var queries = new Mock<IFacilityQueries>();
        var controller = Create(queries);

        var result = await controller.CountFacilities(new FacilityCountRequest
        {
            FacilityIds = Enumerable.Range(0, 5001).Select(index => $"id-{index}").ToList()
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        queries.Verify(item => item.CountAsync(It.IsAny<IReadOnlyCollection<string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Returns_the_aggregate_for_a_normalized_set()
    {
        var expected = new LantanaGroup.Link.Shared.Application.Models.Tenant.FacilityCounts { Total = 9, Matched = 1 };
        var queries = new Mock<IFacilityQueries>();
        queries
            .Setup(item => item.CountAsync(
                It.Is<IReadOnlyCollection<string>?>(ids => ids != null && ids.Count == 1 && ids.Contains("a")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await Create(queries).CountFacilities(
            new FacilityCountRequest { FacilityIds = [" a ", "A"] },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
    }

    private static FacilityController Create(Mock<IFacilityQueries> queries) =>
        new(
            Mock.Of<ILogger<FacilityController>>(),
            Mock.Of<IFacilityManager>(),
            queries.Object,
            Mock.Of<IFacilityOperations>(),
            Mock.Of<IKafkaProducerFactory<string, GenerateReportValue>>(),
            Options.Create(new ServiceRegistry()),
            Mock.Of<IHttpClientFactory>(),
            Options.Create(new LinkTokenServiceSettings()),
            Mock.Of<ICreateSystemToken>(),
            Options.Create(new LinkBearerServiceOptions()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
}
