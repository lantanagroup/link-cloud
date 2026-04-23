using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quartz;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Listeners;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportScheduledListenerTests
{
    private readonly ReportIntegrationTestFixture _fixture;

    public ReportScheduledListenerTests(ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessMessageAsync_NewReport_CreatesScheduleAndSchedulesJob()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();
        _fixture.SchedulerFactoryMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportPopulationManager = scope.ServiceProvider.GetRequiredService<IReportPopulationManager>();

        var facilityId = Guid.NewGuid().ToString();
        var reportId = Guid.NewGuid();

        var startDate = DateTimeOffset.UtcNow.AddDays(-1);
        var endDate = DateTimeOffset.UtcNow.AddDays(30);

        var value = new ReportScheduledValue
        {
            ReportTrackingId = reportId,
            StartDate = startDate,
            EndDate = endDate,
            Frequency = Frequency.Monthly,
            ReportTypes = new List<string> { "DE-111", "DE-222" }
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        var schedulerMock = new Mock<IScheduler>();
        _fixture.SchedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedulerMock.Object);

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        var created = await reportScheduledManager.SingleOrDefaultAsync(x => x.Id == reportId);
        Assert.NotNull(created);
        Assert.Equal(facilityId, created.FacilityId);
        Assert.Equal(ScheduleStatus.Scheduled, created.Status);
        Assert.Equal(Frequency.Monthly, created.Frequency);
        Assert.Equal(2, created.ReportTypes.Count);
        Assert.Equal(startDate, created.ReportStartDate);
        Assert.Equal(endDate, created.ReportEndDate);

        var population = await reportPopulationManager.FindAsync(x => x.ReportScheduleId == reportId);
        Assert.NotNull(population);
        Assert.Equal(2, population.Count);

        var expectedJobData = new Dictionary<string, object>
        {
            { "ReportScheduleId", reportId },
            { "FacilityId", facilityId }
        };

        _fixture.QuartzJobHelperMock.Verify(f => f.ScheduleJob<EndOfReportPeriodJob>(
            expectedJobData,
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_IsValid_Fails_ReportTypesNull_CallsDeadLetterHandler()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();

        var facilityId = Guid.NewGuid().ToString();

        var value = new ReportScheduledValue
        {
            ReportTrackingId = Guid.NewGuid(),
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = null
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.ReportScheduledDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, ReportScheduledValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("Invalid Report Scheduled event")),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_IsValid_Fails_ReportTypesEmpty_CallsDeadLetterHandler()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();

        var facilityId = Guid.NewGuid().ToString();

        var value = new ReportScheduledValue
        {
            ReportTrackingId = Guid.NewGuid(),
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = new List<string>()
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.ReportScheduledDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, ReportScheduledValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("Invalid Report Scheduled event")),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_IsValid_Fails_StartDateDefault_CallsDeadLetterHandler()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();

        var facilityId = Guid.NewGuid().ToString();

        var value = new ReportScheduledValue
        {
            ReportTrackingId = Guid.NewGuid(),
            StartDate = default,
            EndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = new List<string> { "DE-111" }
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.ReportScheduledDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, ReportScheduledValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("Invalid Report Scheduled event")),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_IsValid_Fails_EndDateDefault_CallsDeadLetterHandler()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();

        var facilityId = Guid.NewGuid().ToString();

        var value = new ReportScheduledValue
        {
            ReportTrackingId = Guid.NewGuid(),
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = default,
            Frequency = Frequency.Monthly,
            ReportTypes = new List<string> { "DE-111" }
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.ReportScheduledDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, ReportScheduledValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("Invalid Report Scheduled event")),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_IsValid_Fails_ReportTrackingIdEmpty_CallsDeadLetterHandler()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();

        var facilityId = Guid.NewGuid().ToString();

        var value = new ReportScheduledValue
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-1),
            EndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = new List<string> { "DE-111" }
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.ReportScheduledDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, ReportScheduledValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("Invalid Report Scheduled event")),
                It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_DuplicateReport_CallsDeadLetterHandler()
    {
        _fixture.ReportScheduledDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

        var facilityId = Guid.NewGuid().ToString();
        var existingId = Guid.NewGuid();

        var existing = new ReportScheduleModel
        {
            Id = existingId,
            FacilityId = facilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-1),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = { "DE-111" },
            Status = ScheduleStatus.Scheduled,
            CreateDate = DateTime.UtcNow
        };
        await reportScheduledManager.AddAsync(existing, CancellationToken.None);

        var startDate = DateTimeOffset.UtcNow.AddDays(-1);
        var endDate = DateTimeOffset.UtcNow.AddDays(30);

        var value = new ReportScheduledValue
        {
            ReportTrackingId = existingId,
            StartDate = startDate,
            EndDate = endDate,
            Frequency = Frequency.Monthly,
            ReportTypes = new List<string> { "DE-111" }
        };

        var consumeResult = new ConsumeResult<string, ReportScheduledValue>
        {
            Message = new Message<string, ReportScheduledValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.ReportScheduledDeadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<string, ReportScheduledValue>>(),
                It.Is<DeadLetterException>(ex => ex.Message.Contains("already exists")),
                facilityId),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_NullResult_DoesNothing()
    {
        _fixture.SchedulerFactoryMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<ReportScheduledListener>();

        var consumeResult = (ConsumeResult<string, ReportScheduledValue>)null!;

        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _fixture.SchedulerFactoryMock.Verify(f => f.GetScheduler(It.IsAny<CancellationToken>()), Times.Never);
    }
}
