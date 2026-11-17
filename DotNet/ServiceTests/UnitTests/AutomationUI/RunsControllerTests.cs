
using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class RunsControllerTests
{
    [Theory]
    [InlineData("2026-09-28", "2026-10-13",
        "2026-09-28T00:00:00+00:00", "2026-10-14T00:00:00+00:00")]
    [InlineData("2026-10-13", "2026-10-13",
        "2026-10-13T00:00:00+00:00", "2026-10-14T00:00:00+00:00")]
    [InlineData("2026-10-13", null,
        "2026-10-13T00:00:00+00:00", null)]
    [InlineData(null, "2026-09-28",
        null, "2026-09-29T00:00:00+00:00")]
    [InlineData(null, null, null, null)]
    public async Task RecentRunsPartial_PassesExpectedUtcDateBoundaries(
        string? from,
        string? to,
        string? expectedFrom,
        string? expectedBefore)
    {
        var manager = MockRunManager();
        var sut = Create(manager);

        var createdFrom = ParseDate(from);
        var createdTo = ParseDate(to);
        var fromUtc = ParseTimestamp(expectedFrom);
        var beforeUtc = ParseTimestamp(expectedBefore);

        var result = await sut.RecentRunsPartial(
            createdFrom: createdFrom,
            createdTo: createdTo);

        var partial = result.Should()
            .BeOfType<PartialViewResult>().Subject;

        partial.ViewName.Should().Be("_RecentRunsTable");

        var model = partial.Model.Should()
            .BeOfType<AutomationRunIndexViewModel>().Subject;

        model.CreatedFrom.Should().Be(createdFrom);
        model.CreatedTo.Should().Be(createdTo);

        manager.Verify(m => m.GetRunsPageAsync(
            1,
            20,
            "createdAt",
            true,
            It.IsAny<CancellationToken>(),
            fromUtc,
            beforeUtc), Times.Once);
    }

    [Fact]
    public async Task RecentRunsPartial_PreservesPagingAndSort()
    {
        var manager = MockRunManager();
        var sut = Create(manager);

        await sut.RecentRunsPartial(
            pageNumber: 2,
            pageSize: 10,
            sortBy: "createdAt",
            sortDir: "asc",
            createdFrom: new DateOnly(2026, 9, 28));

        manager.Verify(m => m.GetRunsPageAsync(
            2,
            10,
            "createdAt",
            false,
            It.IsAny<CancellationToken>(),
            new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero),
            null), Times.Once);
    }

    [Fact]
    public async Task RecentRunsPartial_InvalidDateRange_Returns400()
    {
        var manager = MockRunManager();
        var sut = Create(manager);

        var result = await sut.RecentRunsPartial(
            createdFrom: new DateOnly(2026, 10, 14),
            createdTo: new DateOnly(2026, 10, 13));

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>().Subject;

        badRequest.Value.Should()
            .Be("Created From must be on or before Created To.");

        manager.Verify(m => m.GetRunsPageAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    [Fact]
    public async Task RecentRunsPartial_MaximumEndDate_Returns400()
    {
        var manager = MockRunManager();
        var sut = Create(manager);

        var result = await sut.RecentRunsPartial(
            createdTo: DateOnly.MaxValue);

        var badRequest = result.Should()
            .BeOfType<BadRequestObjectResult>().Subject;

        badRequest.Value.Should()
            .Be("Created To is outside the supported date range.");

        manager.Verify(m => m.GetRunsPageAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<DateTimeOffset?>(),
            It.IsAny<DateTimeOffset?>()), Times.Never);
    }

    private static Mock<IAutomationRunManager> MockRunManager()
    {
        var manager = new Mock<IAutomationRunManager>();

        manager.Setup(m => m.GetRunsPageAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<DateTimeOffset?>(),
                It.IsAny<DateTimeOffset?>()))
            .ReturnsAsync(() => new AutomationRunIndexViewModel());

        return manager;
    }

    private static RunsController Create(
        Mock<IAutomationRunManager> manager)
    {
        // The partial action uses only runManager.
        // Other constructor dependencies are not exercised by these tests.
        return new RunsController(
            manager.Object,
            null!, // snapshotStore
            null!, // scenarioStore
            null!, // queryPlanTemplateStore
            null!, // normalizationStore
            null!, // organizationResourceMapTemplateStore
            null!, // patientConfigurationStore
            null!, // measureTemplateStore
            null!, // facilityTemplateStore
            null!, // dataAcqClient
            null!, // runExportService
            null!, // templateCacheVersionStore
            null!, // metricsPresenter
            Mock.Of<ILogger<RunsController>>());
    }

    private static DateOnly? ParseDate(string? value)
        => value is null ? null : DateOnly.Parse(value);

    private static DateTimeOffset? ParseTimestamp(string? value)
        => value is null ? null : DateTimeOffset.Parse(value);
}
