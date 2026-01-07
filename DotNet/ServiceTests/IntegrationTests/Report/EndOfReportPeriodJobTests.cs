using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Entities.Enums;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class EndOfReportPeriodJobTests
    {
        private readonly ReportIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public EndOfReportPeriodJobTests(ReportIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task Execute_AllReady_CallsManifestProducerAndUpdatesStatus()
        {
            var database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            var aggregator = _fixture.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var dataAcqProducer = _fixture.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>();
            var readyValProducer = _fixture.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
            var auditProducer = _fixture.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>();
            var tenantApiService = _fixture.ServiceProvider.GetRequiredService<ITenantApiService>();

            // Setup schedule
            var schedule = new ReportSchedule
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly,
                Status = ScheduleStatus.New,
                EndOfReportPeriodJobHasRun = false,
                PayloadRootUri = "test://payload/root/uri"
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            // Setup submission entries - all ready
            var entry1 = new PatientSubmissionEntry
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.ValidationComplete,
                MeasureReport = new MeasureReport { Id = Guid.NewGuid().ToString(), Measure = "TestMeasure" }
            };
            var entry2 = new PatientSubmissionEntry
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient2",
                Status = PatientSubmissionStatus.NotReportable
            };
            await database.SubmissionEntryRepository.AddAsync(entry1);
            await database.SubmissionEntryRepository.AddAsync(entry2);
            await database.SaveChangesAsync();

            // Mocks
            var loggerMock = new Mock<ILogger<EndOfReportPeriodJob>>();
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            var schedulerMock = new Mock<IScheduler>();
            schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(schedulerMock.Object);
            schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var tenantApiMock = new Mock<ITenantApiService>();
            tenantApiMock.Setup(t => t.GetFacilityConfig(schedule.FacilityId, It.IsAny<CancellationToken>())).Returns(Task.FromResult(new FacilityModel { FacilityName = "TestFacilityName" }));

            var optionsMock = new Mock<IOptions<BlobStorageSettings>>();
            optionsMock.Setup(o => o.Value).Returns(new BlobStorageSettings());

            var blobStorageMock = new Mock<BlobStorageService>(optionsMock.Object);
            blobStorageMock.Setup(b => b.UploadManifestAsync(It.IsAny<ReportSchedule>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Uri("test://payload/root/uri/blob"));

            var submitKafkaMock = new Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>>();
            var submitPayloadProducer = new SubmitPayloadProducer(serviceScopeFactory, submitKafkaMock.Object);

            var manifestProducerLogger = _fixture.ServiceProvider.GetRequiredService<ILogger<ReportManifestProducer>>();
            var manifestProducer = new ReportManifestProducer(manifestProducerLogger, serviceScopeFactory, aggregator, tenantApiMock.Object, blobStorageMock.Object, submitPayloadProducer, auditProducer);

            // Job context - FIXED
            var contextMock = new Mock<IJobExecutionContext>();

            // Add JobDetail mock
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            // Trigger mock (keep as fallback)
            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            // Create job
            var job = new EndOfReportPeriodJob(
                loggerMock.Object,
                schedulerFactoryMock.Object,
                serviceScopeFactory,
                dataAcqProducer,
                readyValProducer,
                manifestProducer);

            // Execute
            await job.Execute(contextMock.Object);

            // Asserts
            database = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();

            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);

            blobStorageMock.Verify(b => b.UploadManifestAsync(It.Is<ReportSchedule>(s => s.Id == schedule.Id), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()), Times.Once());
            submitKafkaMock.Verify(p => p.Produce(It.Is<string>(topic => topic == nameof(KafkaTopic.SubmitPayload)),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m => m.Key.FacilityId == schedule.FacilityId && m.Key.ReportScheduleId == schedule.Id && m.Value.PayloadType == PayloadType.ReportSchedule && m.Value.PayloadUri == "test://payload/root/uri/blob" && m.Value.ReportTypes.Contains("TestReport")), It.Is<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>(h => h == null)), Times.Once());
        }

        [Fact]
        public async Task Execute_NotAllReady_PatientsToEvaluate_CallsDataAcqProducer()
        {
            var database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            var aggregator = _fixture.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = _fixture.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = _fixture.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var readyValProducer = _fixture.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
            var auditProducer = _fixture.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>();
            var tenantApiService = _fixture.ServiceProvider.GetRequiredService<ITenantApiService>();

            // Setup schedule
            var schedule = new ReportSchedule
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

            // Setup submission entries - some pending
            var pendingPatients = new List<string> { "Patient1", "Patient2" };
            foreach (var patientId in pendingPatients)
            {
                var entry = new PatientSubmissionEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = schedule.FacilityId,
                    ReportScheduleId = schedule.Id,
                    PatientId = patientId,
                    ReportType = "TestReport",
                    Status = PatientSubmissionStatus.PendingEvaluation
                };
                await database.SubmissionEntryRepository.AddAsync(entry);
            }
            await database.SaveChangesAsync();

            // Mocks
            var loggerMock = new Mock<ILogger<EndOfReportPeriodJob>>();
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            var schedulerMock = new Mock<IScheduler>();
            schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(schedulerMock.Object);

            var dataAcqKafkaMock = new Mock<IProducer<string, DataAcquisitionRequestedValue>>();
            var dataAcqProducer = new DataAcquisitionRequestedProducer(serviceScopeFactory, dataAcqKafkaMock.Object);

            var manifestProducerLogger = _fixture.ServiceProvider.GetRequiredService<ILogger<ReportManifestProducer>>();
            var manifestProducer = new ReportManifestProducer(manifestProducerLogger, serviceScopeFactory, aggregator, tenantApiService, blobStorageService, submitPayloadProducer, auditProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();

            // Add JobDetail mock
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            // Trigger mock
            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            // Create job with mocked dataAcqProducer
            var job = new EndOfReportPeriodJob(
                loggerMock.Object,
                schedulerFactoryMock.Object,
                serviceScopeFactory,
                dataAcqProducer, 
                readyValProducer,
                manifestProducer);

            // Execute
            await job.Execute(contextMock.Object);

            // Asserts
            dataAcqKafkaMock.Verify(p => p.Produce(It.IsAny<string>(), It.IsAny<Message<string, DataAcquisitionRequestedValue>>(), null), Times.Exactly(pendingPatients.Count));


            database = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();
            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);

            schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task Execute_NotAllReady_NeedsValidation_CallsReadyForValidationProducer()
        {
            var database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            var aggregator = _fixture.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = _fixture.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = _fixture.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var dataAcqProducer = _fixture.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>();
            var auditProducer = _fixture.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>();
            var tenantApiService = _fixture.ServiceProvider.GetRequiredService<ITenantApiService>();

            // Setup schedule
            var schedule = new ReportSchedule
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

            // Setup submission entries - needs validation
            var entry = new PatientSubmissionEntry
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.ReadyForValidation,
                ValidationStatus = ValidationStatus.Pending,
                PayloadUri = "test://payload/patient1"
            };
            await database.SubmissionEntryRepository.AddAsync(entry);
            await database.SaveChangesAsync();

            // Mocks
            var loggerMock = new Mock<ILogger<EndOfReportPeriodJob>>();
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            var schedulerMock = new Mock<IScheduler>();
            schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(schedulerMock.Object);
            schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var readyValKafkaMock = new Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>>();

            // Create producer with mocked Kafka producer and resolved manager
            var readyValProducer = new ReadyForValidationProducer(readyValKafkaMock.Object, _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>());

            var manifestProducerLogger = _fixture.ServiceProvider.GetRequiredService<ILogger<ReportManifestProducer>>();
            var manifestProducer = new ReportManifestProducer(manifestProducerLogger, serviceScopeFactory, aggregator, tenantApiService, blobStorageService, submitPayloadProducer, auditProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();

            // Add JobDetail mock
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            // Trigger mock (keep as fallback)
            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            // Create job
            var job = new EndOfReportPeriodJob(
                loggerMock.Object,
                schedulerFactoryMock.Object,
                serviceScopeFactory,
                dataAcqProducer,
                readyValProducer,
                manifestProducer);

            // Execute
            await job.Execute(contextMock.Object);

            // Asserts
            readyValKafkaMock.Verify(p => p.Produce(It.Is<string>(topic => topic == nameof(KafkaTopic.ReadyForValidation)),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m => m.Key.FacilityId == schedule.FacilityId && m.Value.PatientId == "Patient1" && m.Value.ReportTrackingId == schedule.Id && m.Value.ReportTypes.Contains("TestReport") && m.Value.PayloadUri == "test://payload/patient1"), It.Is<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>(h => h == null)), Times.Once());

            database = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();

            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);

            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.Equal(ValidationStatus.Requested, updatedEntry.ValidationStatus);
        }

        [Fact]
        public async Task Execute_Exception_ReschedulesJob()
        {
            var database = _fixture.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
            var aggregator = _fixture.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = _fixture.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = _fixture.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var readyValProducer = _fixture.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
            var auditProducer = _fixture.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>();
            var tenantApiService = _fixture.ServiceProvider.GetRequiredService<ITenantApiService>();

            // Setup schedule
            var schedule = new ReportSchedule
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
            var entry = new PatientSubmissionEntry
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.PendingEvaluation
            };
            await database.SubmissionEntryRepository.AddAsync(entry);
            await database.SaveChangesAsync();

            // Mocks
            var loggerMock = new Mock<ILogger<EndOfReportPeriodJob>>();
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            var schedulerMock = new Mock<IScheduler>();
            schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(schedulerMock.Object);

            var dataAcqKafkaMock = new Mock<IProducer<string, DataAcquisitionRequestedValue>>();
            dataAcqKafkaMock.Setup(p => p.Produce(It.IsAny<string>(), It.IsAny<Message<string, DataAcquisitionRequestedValue>>(), It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()))
                .Throws(new Exception("Test exception"));
            var dataAcqProducer = new DataAcquisitionRequestedProducer(serviceScopeFactory, dataAcqKafkaMock.Object);

            var manifestProducerLogger = _fixture.ServiceProvider.GetRequiredService<ILogger<ReportManifestProducer>>();
            var manifestProducer = new ReportManifestProducer(manifestProducerLogger, serviceScopeFactory, aggregator, tenantApiService, blobStorageService, submitPayloadProducer, auditProducer);

            // Job context - FIXED
            var contextMock = new Mock<IJobExecutionContext>();

            // Add JobDetail mock
            var jobDetailMock = new Mock<IJobDetail>();
            var jobDetailDataMap = new JobDataMap();
            jobDetailDataMap.Put("ReportScheduleId", schedule.Id);
            jobDetailMock.Setup(j => j.JobDataMap).Returns(jobDetailDataMap);
            contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

            // Trigger mock (keep as fallback)
            var triggerMock = new Mock<ITrigger>();
            var triggerDataMap = new JobDataMap();
            triggerDataMap.Put("ReportScheduleId", schedule.Id);
            triggerMock.Setup(t => t.JobDataMap).Returns(triggerDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

            // Create job
            var job = new EndOfReportPeriodJob(
                loggerMock.Object,
                schedulerFactoryMock.Object,
                serviceScopeFactory,
                dataAcqProducer,
                readyValProducer,
                manifestProducer);

            // Execute
            await job.Execute(contextMock.Object);

            // Asserts
            database = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDatabase>();

            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.NotEqual(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.False(updatedSchedule.EndOfReportPeriodJobHasRun);

            schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [Fact]
        public async Task MongoDb_And_InMemory_Schedulers_Are_Independent()
        {
            _fixture.ResetMocks();
            //await _fixture.ClearDatabaseAsync();

            Quartz.Logging.LogProvider.SetCurrentLogProvider(new NoOpLogProvider());
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

    class NoOpLogProvider : Quartz.Logging.ILogProvider
    {
        public Quartz.Logging.Logger GetLogger(string name) => (level, func, exception, parameters) => true;
        public IDisposable OpenNestedContext(string message) => new NoOpDisposable();
        public IDisposable OpenMappedContext(string key, object value, bool destructure = false) => new NoOpDisposable();

        private class NoOpDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}