using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    public class JobSchedulingIntegrationTests : IClassFixture<ReportIntegrationTestFixture>
    {
        private readonly IServiceProvider _serviceProvider;

        public JobSchedulingIntegrationTests(ReportIntegrationTestFixture fixture)
        {
            _serviceProvider = fixture.ServiceProvider;
        }

        [Fact(DisplayName = "EndOfPeriodReportingJob handles patients to evaluate (calls DataAcquisitionRequestedProducer)")]
        public async Task EndOfPeriodReportingJob_Calls_DataAcqProducer_When_PatientsToEvaluate()
        {
            // Arrange
            var db = _serviceProvider.GetRequiredService<IDatabase>();

            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility2",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                Status = ScheduleStatus.New,
                EndOfReportPeriodJobHasRun = false
            };
            await db.ReportScheduledRepository.AddAsync(schedule);

            // Add a submission entry with PendingEvaluation to trigger DataAcquisitionRequestedProducer
            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient2",
                Status = PatientSubmissionStatus.PendingEvaluation
            };
            await db.SubmissionEntryRepository.AddAsync(entry);

            // Act
            var job = _serviceProvider.GetRequiredService<EndOfReportPeriodJob>();
            var jobDataMap = new JobDataMap { { "ReportScheduleModel", schedule } };
            var triggerMock = new Mock<ITrigger>();
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            var contextMock = new Mock<IJobExecutionContext>();
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);
            await job.Execute(contextMock.Object);

            // Assert: DataAcquisitionRequestedProducer (Kafka) should be called
            var dataAcqProducerMock = ReportIntegrationTestFixture.DataAcquisitionRequestedProducerMock;
            dataAcqProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.DataAcquisitionRequested),
                It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()), Times.Once());
        }

        [Fact(DisplayName = "EndOfPeriodReportingJob handles entries needing validation (calls ReadyForValidationProducer)")]
        public async Task EndOfPeriodReportingJob_Calls_ReadyForValidationProducer_When_ReadyForValidation()
        {
            // Arrange
            var db = _serviceProvider.GetRequiredService<IDatabase>();

            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility3",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                Status = ScheduleStatus.New,
                EndOfReportPeriodJobHasRun = false
            };
            await db.ReportScheduledRepository.AddAsync(schedule);

            // Add a submission entry that needs validation
            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient3",
                Status = PatientSubmissionStatus.ReadyForValidation,
                ValidationStatus = ValidationStatus.Pending,
                PayloadUri = "test://payload/patient3"
            };
            await db.SubmissionEntryRepository.AddAsync(entry);

            // Act
            var job = _serviceProvider.GetRequiredService<EndOfReportPeriodJob>();
            var jobDataMap = new JobDataMap { { "ReportScheduleModel", schedule } };
            var triggerMock = new Mock<ITrigger>();
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            var contextMock = new Mock<IJobExecutionContext>();
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);
            await job.Execute(contextMock.Object);

            // Assert: ReadyForValidationProducer (Kafka) should be called
            var readyForValidationProducerMock = ReportIntegrationTestFixture.ReadyForValidationProducerMock;
            readyForValidationProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.IsAny<Message<ReadyForValidationKey, ReadyForValidationValue>>(),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), Times.Once());
        }

        [Fact(DisplayName = "EndOfPeriodReportingJob handles exception and reschedules job (retry logic)")]
        public async Task EndOfPeriodReportingJob_Reschedules_On_Exception()
        {
            // Arrange
            var db = _serviceProvider.GetRequiredService<IDatabase>();

            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility4",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                Status = ScheduleStatus.New,
                EndOfReportPeriodJobHasRun = false
            };
            await db.ReportScheduledRepository.AddAsync(schedule);

            // Add a submission entry to trigger manifest production
            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient4",
                Status = PatientSubmissionStatus.ValidationComplete,
                MeasureReport = new MeasureReport { Id = Guid.NewGuid().ToString(), Measure = "TestMeasure" }
            };
            await db.SubmissionEntryRepository.AddAsync(entry);

            // Simulate exception by making BlobStorageService.UploadManifestAsync throw
            var blobStorageMock = ReportIntegrationTestFixture.GetBlobStorageMock();
            blobStorageMock
                .Setup(b => b.UploadManifestAsync(
                    It.IsAny<ReportScheduleModel>(),
                    It.IsAny<IEnumerable<Hl7.Fhir.Model.Resource>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated failure"));

            // Act
            var job = _serviceProvider.GetRequiredService<EndOfReportPeriodJob>();
            var jobDataMap = new JobDataMap { { "ReportScheduleModel", schedule } };
            var triggerMock = new Mock<ITrigger>();
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            var contextMock = new Mock<IJobExecutionContext>();
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);
            await job.Execute(contextMock.Object);

            // Assert: Schedule should not be marked as completed, and status should NOT be EndOfPeriod
            var updatedSchedule = await db.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.NotEqual(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.False(updatedSchedule.EndOfReportPeriodJobHasRun);

            // Assert: The scheduler's RescheduleJob should have been called
            var schedulerFactoryMock = ReportIntegrationTestFixture.GetSchedulerFactoryMock();
            schedulerFactoryMock.Verify(f => f.GetScheduler(It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }

        [Fact(DisplayName = "RetryJob and EndOfReportPeriodJob are scheduled independently")]
        public async Task Jobs_Are_Scheduled_Independently()
        {
            // Arrange: use the real in-memory Quartz scheduler
            ISchedulerFactory schedulerFactory = new StdSchedulerFactory();
            IScheduler scheduler = await schedulerFactory.GetScheduler();
            await scheduler.Clear(); // Just in case

            // Define jobs
            IJobDetail retryJob = JobBuilder.Create<RetryJob>()
                .WithIdentity("RetryJob", "RetryGroup")
                .Build();

            IJobDetail endJob = JobBuilder.Create<EndOfReportPeriodJob>()
                .WithIdentity("EndOfReportPeriodJob", "ReportGroup")
                .Build();

            // Define triggers
            ITrigger retryTrigger = TriggerBuilder.Create()
                .WithIdentity("RetryTrigger", "RetryGroup")
                .StartNow()
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(10).RepeatForever())
                .ForJob(retryJob)
                .Build();

            ITrigger endTrigger = TriggerBuilder.Create()
                .WithIdentity("EndTrigger", "ReportGroup")
                .StartNow()
                .WithDailyTimeIntervalSchedule(x => x.OnEveryDay().StartingDailyAt(Quartz.TimeOfDay.HourAndMinuteOfDay(1, 0)))
                .ForJob(endJob)
                .Build();

            // Schedule jobs
            await scheduler.ScheduleJob(retryJob, retryTrigger);
            await scheduler.ScheduleJob(endJob, endTrigger);

            // Act: Query jobs and triggers by group
            var retryJobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("RetryGroup"));
            var reportJobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("ReportGroup"));
            var retryTriggers = await scheduler.GetTriggersOfJob(new JobKey("RetryJob", "RetryGroup"));
            var endTriggers = await scheduler.GetTriggersOfJob(new JobKey("EndOfReportPeriodJob", "ReportGroup"));

            // Assert: jobs and triggers are independent
            Assert.Single(retryJobKeys);
            Assert.Single(reportJobKeys);
            Assert.Single(retryTriggers);
            Assert.Single(endTriggers);

            Assert.NotEqual(retryTriggers.First().Key, endTriggers.First().Key);
            Assert.DoesNotContain(new JobKey("EndOfReportPeriodJob", "ReportGroup"), retryJobKeys);
            Assert.DoesNotContain(new JobKey("RetryJob", "RetryGroup"), reportJobKeys);
        }
    }
}

// Dummy job implementations for test purposes
public class RetryJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // Simulate work
        return Task.CompletedTask;
    }
}

public class DummyEndOfReportPeriodJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // Simulate work
        return Task.CompletedTask;
    }
}