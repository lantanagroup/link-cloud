using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Terminology;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Models;
using LantanaGroup.Link.Terminology.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Terminology;

/// <summary>
/// Covers the two failure paths of <see cref="CodesController"/> that a request over HTTP cannot produce
/// reliably: a cancelled search and an unexpected fault in the service.
/// </summary>
/// <remarks>
/// The action is called directly. No <see cref="Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory"/>
/// is wired up, which is the unit-testing scenario <c>ControllerBase.Problem</c> handles by building a plain
/// <see cref="ProblemDetails"/> from its arguments - the same approach <c>ConfigControllerTests</c> takes.
/// </remarks>
public class CodesControllerTests
{
    private readonly Mock<ICodeSearchService> _service = new();
    private readonly Mock<ILogger<CodesController>> _logger = new();

    private CodesController BuildController() => new(_service.Object, _logger.Object);

    private static CodeSearchQuery ValidQuery() => new() { Search = "burn" };

    /// <summary>
    /// The caller gave up mid-scan, so there is nobody to answer. Reporting a server fault would log a
    /// defect that never happened and hide real ones; the exception has to reach the pipeline intact.
    /// </summary>
    [Fact]
    public async Task Search_WhenCancelled_LetsTheCancellationPropagate()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _service
            .Setup(x => x.Search(It.IsAny<CodeSearchQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var controller = BuildController();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.Search(ValidQuery(), cts.Token));

        // A cancelled request is not a fault, so nothing should have been logged as one.
        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Anything else is a defect: answer 500 and log it, rather than letting the caller see the exception.
    /// </summary>
    [Fact]
    public async Task Search_WhenTheServiceFaults_Returns500AndLogsIt()
    {
        _service
            .Setup(x => x.Search(It.IsAny<CodeSearchQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("the cache exploded"));

        var controller = BuildController();

        var result = await controller.Search(ValidQuery());

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);

        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// The token the caller supplied has to reach the service, or a cancelled request keeps scanning.
    /// </summary>
    [Fact]
    public async Task Search_ForwardsTheCancellationTokenToTheService()
    {
        using var cts = new CancellationTokenSource();

        _service
            .Setup(x => x.Search(It.IsAny<CodeSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<TerminologyCodeModel>([], new PaginationMetadata(20, 1, 0)));

        var controller = BuildController();

        await controller.Search(ValidQuery(), cts.Token);

        _service.Verify(x => x.Search(It.IsAny<CodeSearchQuery>(), cts.Token), Times.Once);
    }
}
