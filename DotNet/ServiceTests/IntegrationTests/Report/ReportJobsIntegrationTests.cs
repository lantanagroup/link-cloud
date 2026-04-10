using Confluent.Kafka;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Jobs;

[Collection("ReportIntegrationTests")]
public class EndOfReportPeriodJobTests
{
    private readonly IntegrationTests.Report.ReportIntegrationTestFixture _fixture;

    public EndOfReportPeriodJobTests(IntegrationTests.Report.ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Execute_UpdatesScheduleStatusAndDeletesJob()
    {
        _fixture.QuartzJobHelperMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var scheduleId = Guid.NewGuid();
        var facilityId = "test-facility";

        var schedule = new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = facilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(-1),
            Status = ScheduleStatus.Scheduled,
            EndOfReportPeriodJobHasRun = false
        };
        await database.ReportScheduledRepository.AddAsync(schedule);

        var reportEntry = new ReportEntry
        {
            Id = Guid.NewGuid(),
            CreateDate = DateTime.UtcNow,
            FacilityId = facilityId,
            ReportScheduleId = scheduleId,
            PatientId = "test-patient",
            ReportingStatus = ReportingStatus.PatientIdentified,
            SubmissionStatus = null
        };
        await database.ReportEntryRepository.AddAsync(reportEntry);

        await database.ReportPopulationRepository.SaveChangesAsync();

        _fixture.FacilityServiceClientMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityModel { FacilityId = facilityId });

        _fixture.SubmitPayloadKafkaProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<SubmitPayloadKey, SubmitPayloadValue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<SubmitPayloadKey, SubmitPayloadValue>
            {
                Status = PersistenceStatus.Persisted
            });

        _fixture.AuditableEventKafkaProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, AuditEventMessage>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, AuditEventMessage>
            {
                Status = PersistenceStatus.Persisted
            });

        _fixture.DataAcquisitionRequestedKafkaProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, DataAcquisitionRequestedValue>
            {
                Status = PersistenceStatus.Persisted
            });

        var jobDataMap = new JobDataMap
        {
            { "ReportScheduleId", JsonSerializer.Serialize(scheduleId) }
        };

        var jobKey = new JobKey("test-end-of-period-job", "test-group");

        _fixture.QuartzJobHelperMock
            .Setup(q => q.DeleteJob(
                jobKey.Name,
                jobKey.Group,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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
            It.IsAny<CancellationToken>()),
            Times.Once());

        using var assertScope = _fixture.ScopeFactory.CreateScope();
        var assertDatabase = assertScope.ServiceProvider.GetRequiredService<IDatabase>();
        var updatedSchedule = await assertDatabase.ReportScheduledRepository.GetAsync(scheduleId);

        Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
        Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);
    }

    [Fact]
    public async Task Execute_OnException_ReschedulesJob()
    {
        _fixture.QuartzJobHelperMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var scheduleId = Guid.NewGuid();

        var schedule = new ReportSchedule
        {
            Id = scheduleId,
            FacilityId = "test-facility-error",
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(-1),
            Status = ScheduleStatus.Scheduled
        };
        await database.ReportScheduledRepository.AddAsync(schedule);
        await database.ReportPopulationRepository.SaveChangesAsync();

        var jobDataMap = new JobDataMap
        {
            { "ReportScheduleId", scheduleId }
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
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated delete failure"));

        var job = new EndOfReportPeriodJob(
            scope.ServiceProvider.GetRequiredService<ILogger<EndOfReportPeriodJob>>(),
            _fixture.QuartzJobHelperMock.Object,
            _fixture.ScopeFactory,
            scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>());

        await job.Execute(contextMock.Object);

        _fixture.QuartzJobHelperMock.Verify(q => q.RescheduleJob<EndOfReportPeriodJob>(
            It.IsAny<string>(),
            It.IsAny<IDictionary<string, object>>(),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce());
    }
}