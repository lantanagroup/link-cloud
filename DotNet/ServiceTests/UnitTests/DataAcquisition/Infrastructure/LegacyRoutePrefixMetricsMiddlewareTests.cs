using FluentAssertions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Moq;
using AspNetEndpoint = Microsoft.AspNetCore.Http.Endpoint;
using Task = System.Threading.Tasks.Task;

namespace ServiceTests.UnitTests.DataAcquisition.Infrastructure;

/// <summary>
/// Covers when a legacy-prefix request is counted, what it is tagged with, and what is logged.
/// </summary>
[Trait("Category", "UnitTests")]
public class LegacyRoutePrefixMetricsMiddlewareTests
{
    private const string RouteTemplate = "api/data-acquisition/{facilityId}/QueryPlan/All";
    private const string FacilityId = "fac-raw-id-123";

    private readonly Mock<IDataAcquisitionServiceMetrics> _metrics = new();
    private readonly Mock<ILogger<LegacyRoutePrefixMetricsMiddleware>> _logger = new();

    private bool _nextCalled;

    public LegacyRoutePrefixMetricsMiddlewareTests()
    {
        _logger
            .Setup(l => l.IsEnabled(It.IsAny<LogLevel>()))
            .Returns(true);
    }

    [Fact]
    public async Task InvokeAsync_LegacyRequestWithRouteEndpoint_CountsRouteTemplateAndMethod()
    {
        var context = CreateContext(legacy: true, HttpMethods.Get, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _metrics.Verify(m => m.IncrementPathRewriteCounter(RouteTemplate, HttpMethods.Get), Times.Once);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task InvokeAsync_LegacyRequest_TagsRequestMethod(string method)
    {
        var context = CreateContext(legacy: true, method, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _metrics.Verify(m => m.IncrementPathRewriteCounter(RouteTemplate, method), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_LegacyRequest_CallsNext()
    {
        var context = CreateContext(legacy: true, HttpMethods.Get, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_LegacyRequestWithNoEndpoint_CountsAsUnmatched()
    {
        // A legacy caller hitting a path that no longer exists is still a caller to find before the alias goes.
        var context = CreateContext(legacy: true, HttpMethods.Get, endpoint: null);

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _metrics.Verify(m => m.IncrementPathRewriteCounter("unmatched", HttpMethods.Get), Times.Once);
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_LegacyRequestWithNonRouteEndpoint_CountsAsUnmatched()
    {
        var endpoint = new AspNetEndpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "not a route");
        var context = CreateContext(legacy: true, HttpMethods.Get, endpoint);

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _metrics.Verify(m => m.IncrementPathRewriteCounter("unmatched", HttpMethods.Get), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CanonicalRequest_DoesNotCount()
    {
        var context = CreateContext(legacy: false, HttpMethods.Get, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _metrics.Verify(m => m.IncrementPathRewriteCounter(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_CanonicalRequest_DoesNotLog()
    {
        var context = CreateContext(legacy: false, HttpMethods.Get, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        VerifyLogged(It.IsAny<LogLevel>(), Times.Never());
    }

    [Fact]
    public async Task InvokeAsync_LegacyRequest_LogsOnceAtDebug()
    {
        // The Admin UI polls acquisition logs, so anything above Debug floods Loki until the UI moves over.
        var context = CreateContext(legacy: true, HttpMethods.Get, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        VerifyLogged(LogLevel.Debug, Times.Once());
        VerifyLogged(It.Is<LogLevel>(level => level > LogLevel.Debug), Times.Never());
    }

    [Fact]
    public async Task InvokeAsync_LegacyRequest_LogsTemplateNotRawPath()
    {
        // Raw paths carry facility and patient ids; the template carries neither.
        var context = CreateContext(legacy: true, HttpMethods.Get, CreateRouteEndpoint(RouteTemplate));

        await CreateMiddleware().InvokeAsync(context, _metrics.Object);

        _logger.Verify(l => l.Log(LogLevel.Debug,
                                  It.IsAny<EventId>(),
                                  It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(RouteTemplate) &&
                                                                    !state.ToString()!.Contains(FacilityId)),
                                  It.IsAny<Exception?>(),
                                  It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                       Times.Once);
    }

    private LegacyRoutePrefixMetricsMiddleware CreateMiddleware()
    {
        return new LegacyRoutePrefixMetricsMiddleware(_ =>
                                                      {
                                                          _nextCalled = true;
                                                          return Task.CompletedTask;
                                                      },
                                                      _logger.Object);
    }

    private static DefaultHttpContext CreateContext(bool legacy, string method, AspNetEndpoint? endpoint)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = $"/api/data-acquisition/{FacilityId}/QueryPlan/All";
        context.SetEndpoint(endpoint);

        if (legacy)
        {
            context.Items[LegacyRoutePrefixRule.LegacyRequestItemKey] = true;
        }

        return context;
    }

    private static RouteEndpoint CreateRouteEndpoint(string template)
    {
        return new RouteEndpoint(_ => Task.CompletedTask,
                                 RoutePatternFactory.Parse(template),
                                 order: 0,
                                 EndpointMetadataCollection.Empty,
                                 template);
    }

    private void VerifyLogged(LogLevel level, Times times)
    {
        _logger.Verify(l => l.Log(level,
                                  It.IsAny<EventId>(),
                                  It.IsAny<It.IsAnyType>(),
                                  It.IsAny<Exception?>(),
                                  It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                       times);
    }
}
