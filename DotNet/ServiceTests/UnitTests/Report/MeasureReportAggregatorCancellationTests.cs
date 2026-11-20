using System.Linq.Expressions;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// <see cref="MeasureReportAggregator.CreateMeasureReportAggregate"/> materializes one subject-list entry
/// per patient MeasureReport after its single query returns, so cancellation has to be honoured inside
/// those loops, not only by the query.
/// </summary>
[Trait("Category", "UnitTests")]
public class MeasureReportAggregatorCancellationTests
{
    /// <summary>
    /// Cancellation lands while the first group is being built: an unreadable population code makes the
    /// aggregator log a warning, and that warning cancels the token. Every later group and population
    /// also has an unreadable code, so had processing continued, each would have logged its own warning.
    /// </summary>
    [Fact]
    public async Task CreateMeasureReportAggregate_CancelledDuringMaterialization_StopsBeforeTheNextPatient()
    {
        using var cts = new CancellationTokenSource();
        var logger = new Mock<ILogger<MeasureReportAggregator>>();
        logger
            .Setup(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => cts.Cancel());

        var populations = new Mock<IReportPopulationManager>();
        populations
            .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportPopulation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Population("measure-a"), Population("measure-b")]);

        var aggregator = new MeasureReportAggregator(logger.Object, populations.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => aggregator.CreateMeasureReportAggregate(Schedule(), "org-1", cts.Token));

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        logger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    /// <summary>
    /// Cancelled after the query returns but before anything is built: no aggregate is produced at all.
    /// </summary>
    [Fact]
    public async Task CreateMeasureReportAggregate_CancelledAfterQuery_BuildsNothing()
    {
        using var cts = new CancellationTokenSource();
        var logger = new Mock<ILogger<MeasureReportAggregator>>();
        var populations = new Mock<IReportPopulationManager>();
        populations
            .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportPopulation, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync([Population("measure-a")]);

        var aggregator = new MeasureReportAggregator(logger.Object, populations.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => aggregator.CreateMeasureReportAggregate(Schedule(), "org-1", cts.Token));

        logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateMeasureReportAggregate_NotCancelled_BuildsEveryPatientEntry()
    {
        var populations = new Mock<IReportPopulationManager>();
        populations
            .Setup(m => m.FindAsync(It.IsAny<Expression<Func<ReportPopulation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Population("measure-a"), Population("measure-b")]);

        var aggregator = new MeasureReportAggregator(Mock.Of<ILogger<MeasureReportAggregator>>(), populations.Object);

        var result = await aggregator.CreateMeasureReportAggregate(Schedule(), "org-1", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, mr =>
        {
            Assert.Equal(2, mr.Contained.Count);
            Assert.All(mr.Contained.OfType<Hl7.Fhir.Model.List>(), list => Assert.Equal(3, list.Entry.Count));
        });
    }

    private static ReportScheduleModel Schedule() => new()
    {
        Id = Guid.NewGuid(),
        FacilityId = "facility-a",
        ReportStartDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ReportEndDate = new DateTimeOffset(2025, 1, 31, 23, 59, 59, TimeSpan.Zero)
    };

    /// <summary>
    /// Two groups of three patient MeasureReports each. The population code is not valid JSON, so
    /// building each group logs one warning.
    /// </summary>
    private static ReportPopulationModel Population(string measure) => new()
    {
        Id = Guid.NewGuid(),
        FacilityId = "facility-a",
        Measure = measure,
        ReportType = measure,
        GroupPopulations =
        [
            Group($"{measure}-initial-population"),
            Group($"{measure}-denominator")
        ]
    };

    private static GroupPopulationModel Group(string populationId) => new()
    {
        PopulationId = populationId,
        PopulationCodeJson = "not json",
        TotalPopulationCount = 3,
        MeasureReportPopulations =
        [
            new MeasureReportPopulationModel { MeasureReportId = $"{populationId}-mr-1", PopulationCount = 1 },
            new MeasureReportPopulationModel { MeasureReportId = $"{populationId}-mr-2", PopulationCount = 1 },
            new MeasureReportPopulationModel { MeasureReportId = $"{populationId}-mr-3", PopulationCount = 1 }
        ]
    };
}
