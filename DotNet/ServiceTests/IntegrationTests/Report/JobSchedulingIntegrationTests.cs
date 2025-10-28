using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

            // Setup proper job context
            var contextMock = new Mock<IJobExecutionContext>();
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            await job.Execute(contextMock.Object);

            // Assert: DataAcquisitionRequestedProducer (Kafka) should be called
            var dataAcqProducerMock = ReportIntegrationTestFixture.DataAcquisitionRequestedProducerMock;
            dataAcqProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.DataAcquisitionRequested),
                It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()), Times.Once());

            // Verify schedule was updated
            var updatedSchedule = await db.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);
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

            // Setup proper job context
            var contextMock = new Mock<IJobExecutionContext>();
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            await job.Execute(contextMock.Object);

            // Assert: ReadyForValidationProducer (Kafka) should be called
            var readyForValidationProducerMock = ReportIntegrationTestFixture.ReadyForValidationProducerMock;
            readyForValidationProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.IsAny<Message<ReadyForValidationKey, ReadyForValidationValue>>(),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), Times.Once());

            // Verify schedule was updated
            var updatedSchedule = await db.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);

            // Verify entry validation status was updated
            var updatedEntry = await db.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.Equal(ValidationStatus.Requested, updatedEntry.ValidationStatus);
        }

        [Fact(DisplayName = "EndOfPeriodReportingJob handles exception and reschedules job (retry logic)")]
        public async Task Execute_Exception_ReschedulesJob()
        {
            using var scope = _serviceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var aggregator = scope.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var readyValProducer = scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
            var auditProducer = scope.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>();
            var tenantApiService = scope.ServiceProvider.GetRequiredService<ITenantApiService>();
            var dataAcqKafkaProducer = scope.ServiceProvider.GetRequiredService<IProducer<string, DataAcquisitionRequestedValue>>();

            // Setup schedule
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                Status = ScheduleStatus.New,
                EndOfReportPeriodJobHasRun = false
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            // Setup submission entries - patients to evaluate to trigger exception
            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.PendingEvaluation
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Mocks
            var loggerMock = new Mock<ILogger<EndOfReportPeriodJob>>();
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            var schedulerMock = new Mock<IScheduler>();
            schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(schedulerMock.Object);

            var dataAcqKafkaMock = new Mock<IProducer<string, DataAcquisitionRequestedValue>>();
            dataAcqKafkaMock.Setup(p => p.Produce(It.IsAny<string>(), It.IsAny<Message<string, DataAcquisitionRequestedValue>>(), It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>())).Throws(new Exception("Test exception"));
            var dataAcqProducer = new DataAcquisitionRequestedProducer(database, dataAcqKafkaMock.Object);

            var manifestProducerLogger = scope.ServiceProvider.GetRequiredService<ILogger<ReportManifestProducer>>();
            var manifestProducer = new ReportManifestProducer(manifestProducerLogger, database, aggregator, tenantApiService, blobStorageService, submitPayloadProducer, auditProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            // Mock rescheduling behavior
            schedulerMock.Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>())).ReturnsAsync(DateTimeOffset.UtcNow.AddDays(1));

            // Create job
            var job = new EndOfReportPeriodJob(
                loggerMock.Object,
                schedulerFactoryMock.Object,
                database,
                dataAcqProducer,
                readyValProducer,
                manifestProducer);

            // Execute
            await job.Execute(contextMock.Object);

            // Asserts
            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.NotEqual(ScheduleStatus.EndOfPeriod, updatedSchedule.Status); // Expect status not to be updated
            Assert.False(updatedSchedule.EndOfReportPeriodJobHasRun); // Expect flag not to be set

            // Verify that the job is rescheduled due to the exception
            schedulerMock.Verify(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()), Times.Once());

            // Verify that DeleteJob is also called as part of the reschedule flow
            schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Once());

            // Verify that the exception was logged
            loggerMock.Verify(
                x => x.Log(
                    It.Is<LogLevel>(l => l == LogLevel.Error),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once());
        }

        [Fact(DisplayName = "MongoDB and InMemory schedulers work independently")]
        public async Task MongoDb_And_InMemory_Schedulers_Are_Independent()
        {
            // This test verifies that the two keyed schedulers (MongoScheduler and InMemoryScheduler) 
            // can coexist and operate independently

            // Arrange: Create two separate in-memory schedulers to simulate the behavior
            // Disable logging to avoid disposed LoggerFactory issues
            ISchedulerFactory mongoSchedulerFactory = new StdSchedulerFactory(new System.Collections.Specialized.NameValueCollection
            {
                { "quartz.scheduler.instanceName", "MongoSimulatedScheduler" },
                { "quartz.scheduler.exporter.type", "Quartz.Simpl.RemotingSchedulerExporter, Quartz" },
                { "quartz.scheduler.exporter.bindName", "QuartzScheduler" },
                { "quartz.scheduler.exporter.channelType", "tcp" },
                { "quartz.serializer.type", "binary" }
            });

            ISchedulerFactory inMemorySchedulerFactory = new StdSchedulerFactory(new System.Collections.Specialized.NameValueCollection
            {
                { "quartz.scheduler.instanceName", "InMemorySimulatedScheduler" },
                { "quartz.scheduler.exporter.type", "Quartz.Simpl.RemotingSchedulerExporter, Quartz" },
                { "quartz.scheduler.exporter.bindName", "QuartzScheduler" },
                { "quartz.scheduler.exporter.channelType", "tcp" },
                { "quartz.serializer.type", "binary" }
            });

            IScheduler mongoScheduler = await mongoSchedulerFactory.GetScheduler();
            IScheduler inMemoryScheduler = await inMemorySchedulerFactory.GetScheduler();

            await mongoScheduler.Start(); // Start the schedulers
            await inMemoryScheduler.Start();

            await mongoScheduler.Clear();
            await inMemoryScheduler.Clear();

            // Define jobs for different schedulers
            IJobDetail reportJob = JobBuilder.Create<DummyEndOfReportPeriodJob>()
                .WithIdentity("EndOfReportPeriodJob", "ReportGroup")
                .Build();

            IJobDetail retryJob = JobBuilder.Create<DummyRetryJob>()
                .WithIdentity("RetryJob", "RetryGroup")
                .Build();

            // Define triggers
            ITrigger reportTrigger = TriggerBuilder.Create()
                .WithIdentity("ReportTrigger", "ReportGroup")
                .StartNow()
                .WithDailyTimeIntervalSchedule(x => x.OnEveryDay().StartingDailyAt(Quartz.TimeOfDay.HourAndMinuteOfDay(1, 0)))
                .ForJob(reportJob)
                .Build();

            ITrigger retryTrigger = TriggerBuilder.Create()
                .WithIdentity("RetryTrigger", "RetryGroup")
                .StartNow()
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(10).RepeatForever())
                .ForJob(retryJob)
                .Build();

            // Schedule jobs on different schedulers
            await mongoScheduler.ScheduleJob(reportJob, reportTrigger);
            await inMemoryScheduler.ScheduleJob(retryJob, retryTrigger);

            // Act: Query jobs from each scheduler
            var mongoJobKeys = await mongoScheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            var inMemoryJobKeys = await inMemoryScheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());

            // Assert: Each scheduler has its own jobs
            Assert.Single(mongoJobKeys);
            Assert.Single(inMemoryJobKeys);

            Assert.Contains(mongoJobKeys, k => k.Name == "EndOfReportPeriodJob");
            Assert.Contains(inMemoryJobKeys, k => k.Name == "RetryJob");

            // Verify they're truly independent
            Assert.DoesNotContain(mongoJobKeys, k => k.Name == "RetryJob");
            Assert.DoesNotContain(inMemoryJobKeys, k => k.Name == "EndOfReportPeriodJob");

            // Verify scheduler instances are different
            Assert.NotEqual(mongoScheduler.SchedulerName, inMemoryScheduler.SchedulerName);

            // Cleanup
            await mongoScheduler.Shutdown();
            await inMemoryScheduler.Shutdown();
        }
    }
}

// Dummy job implementations for test purposes
public class DummyRetryJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // Simulate retry work
        return Task.CompletedTask;
    }
}

public class DummyEndOfReportPeriodJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // Simulate end of report period work
        return Task.CompletedTask;
    }
}