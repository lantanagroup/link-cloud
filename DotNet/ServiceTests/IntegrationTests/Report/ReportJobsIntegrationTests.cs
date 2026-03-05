using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report;

[Collection("ReportIntegrationTests")]
public class EndOfReportPeriodJobTests
{
    private readonly ReportIntegrationTestFixture _fixture;

    public EndOfReportPeriodJobTests(ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Execute_UpdatesScheduleStatusAndDeletesJob()
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var scheduleId = System.Guid.NewGuid().ToString();
        var facilityId = "test-facility";

        var schedule = new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = facilityId,
            ReportStartDate = System.DateTime.UtcNow.AddDays(-30),
            ReportEndDate = System.DateTime.UtcNow.AddDays(-1),
            Status = ScheduleStatus.Scheduled,
            EndOfReportPeriodJobHasRun = false
        };
        await database.ReportScheduledRepository.AddAsync(schedule);
        await database.ReportPopulationRepository.SaveChangesAsync();

        _fixture.TenantApiServiceMock.Setup(t => t.GetFacilityConfig(Moq.It.IsAny<string>(), Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new FacilityModel { FacilityId = facilityId });

        _fixture.SubmitPayloadKafkaProducerMock
            .Setup(p => p.ProduceAsync(
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<Message<SubmitPayloadKey, SubmitPayloadValue>>(),
                Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<SubmitPayloadKey, SubmitPayloadValue>
            {
                Status = PersistenceStatus.Persisted
            });

        _fixture.AuditableEventKafkaProducerMock
            .Setup(p => p.ProduceAsync(
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<Message<string, AuditEventMessage>>(),
                Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, AuditEventMessage>
            {
                Status = PersistenceStatus.Persisted
            });

        _fixture.DataAcquisitionRequestedKafkaProducerMock
            .Setup(p => p.ProduceAsync(
                Moq.It.IsAny<string>(),
                Moq.It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, DataAcquisitionRequestedValue>
            {
                Status = PersistenceStatus.Persisted
            });

        var jobDataMap = new JobDataMap
        {
            { "ReportScheduleId", JsonSerializer.Serialize(scheduleId) }
        };

        var jobKey = new JobKey("test-end-of-period-job", "test-group");

        var jobDetailMock = new Mock<IJobDetail>();
        jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDataMap);
        jobDetailMock.Setup(j => j.Key).Returns(jobKey);
        jobDetailMock.Setup(j => j.Description).Returns("End of Report Period Job");

        var triggerMock = new Mock<ITrigger>();
        triggerMock.Setup(t => t.JobDataMap).Returns(new JobDataMap());

        var contextMock = new Mock<IJobExecutionContext>();
        contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);
        contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

        var job = new EndOfReportPeriodJob(
            scope.ServiceProvider.GetRequiredService<ILogger<EndOfReportPeriodJob>>(),
            _fixture.QuartzJobHelperMock.Object,
            _fixture.ScopeFactory,
            scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>());

        await job.Execute(contextMock.Object);

        _fixture.QuartzJobHelperMock.Verify(q => q.DeleteJob(
            jobKey.Name,
            jobKey.Group,
            Moq.It.IsAny<System.Threading.CancellationToken>()),
            Moq.Times.Once());

        using var assertScope = _fixture.ScopeFactory.CreateScope();
        var assertDatabase = assertScope.ServiceProvider.GetRequiredService<IDatabase>();
        var updatedSchedule = await assertDatabase.ReportScheduledRepository.GetAsync(scheduleId);

        Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
        Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);
    }

    [Fact]
    public async Task Execute_OnException_ReschedulesJob()
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var scheduleId = System.Guid.NewGuid().ToString();

        var schedule = new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = "test-facility-error",
            ReportStartDate = System.DateTime.UtcNow.AddDays(-30),
            ReportEndDate = System.DateTime.UtcNow.AddDays(-1),
            Status = ScheduleStatus.Scheduled
        };
        await database.ReportScheduledRepository.AddAsync(schedule);
        await database.ReportPopulationRepository.SaveChangesAsync();

        var jobDataMap = new JobDataMap
        {
            { "ReportScheduleId", JsonSerializer.Serialize(scheduleId) }
        };

        var jobKey = new JobKey("test-end-of-period-error-job", "test-group");

        var jobDetailMock = new Mock<IJobDetail>();
        jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDataMap);
        jobDetailMock.Setup(j => j.Key).Returns(jobKey);
        jobDetailMock.Setup(j => j.Description).Returns("End of Report Period Job");

        var triggerMock = new Mock<ITrigger>();
        triggerMock.Setup(t => t.JobDataMap).Returns(new JobDataMap());

        var contextMock = new Mock<IJobExecutionContext>();
        contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);
        contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

        _fixture.QuartzJobHelperMock.Setup(q => q.DeleteJob(
            Moq.It.IsAny<string>(),
            Moq.It.IsAny<string>(),
            Moq.It.IsAny<System.Threading.CancellationToken>()))
            .ThrowsAsync(new System.Exception("Simulated delete failure"));

        var job = new EndOfReportPeriodJob(
            scope.ServiceProvider.GetRequiredService<ILogger<EndOfReportPeriodJob>>(),
            _fixture.QuartzJobHelperMock.Object,
            _fixture.ScopeFactory,
            scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>());

        await job.Execute(contextMock.Object);

        _fixture.QuartzJobHelperMock.Verify(q => q.RescheduleJob<EndOfReportPeriodJob>(
            Moq.It.IsAny<string>(),
            Moq.It.IsAny<System.Collections.Generic.IDictionary<string, object>>(),
            Moq.It.IsAny<System.DateTimeOffset>(),
            Moq.It.IsAny<string>(),
            Moq.It.IsAny<string>(),
            Moq.It.IsAny<System.Threading.CancellationToken>()),
            Moq.Times.AtLeastOnce());
    }
}