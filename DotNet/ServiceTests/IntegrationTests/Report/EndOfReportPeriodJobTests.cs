using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Jobs;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
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
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var aggregator = scope.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var dataAcqProducer = scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>();
            var readyValProducer = scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
            var tenantApiService = scope.ServiceProvider.GetRequiredService<ITenantApiService>();

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
                EndOfReportPeriodJobHasRun = false,
                PayloadRootUri = "test://payload/root/uri"
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            // Setup submission entries - all ready
            var entry1 = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                Status = PatientSubmissionStatus.ValidationComplete,
                MeasureReport = new MeasureReport { Id = Guid.NewGuid().ToString(), Measure = "TestMeasure" }
            };
            var entry2 = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient2",
                Status = PatientSubmissionStatus.NotReportable
            };
            await database.SubmissionEntryRepository.AddAsync(entry1);
            await database.SubmissionEntryRepository.AddAsync(entry2);

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
            blobStorageMock.Setup(b => b.UploadManifestAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Uri("test://payload/root/uri/blob"));

            var submitKafkaMock = new Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>>();
            var submitPayloadProducer = new SubmitPayloadProducer(database, submitKafkaMock.Object);

            var manifestProducer = new ReportManifestProducer(database, aggregator, tenantApiMock.Object, blobStorageMock.Object, submitPayloadProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();
            var triggerMock = new Mock<ITrigger>();
            var jobDataMap = new JobDataMap();
            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, schedule);
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

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
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);

            blobStorageMock.Verify(b => b.UploadManifestAsync(It.Is<ReportScheduleModel>(s => s.Id == schedule.Id), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()), Times.Once());
            submitKafkaMock.Verify(p => p.Produce(It.Is<string>(topic => topic == nameof(KafkaTopic.SubmitPayload)),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m => m.Key.FacilityId == schedule.FacilityId && m.Key.ReportScheduleId == schedule.Id && m.Value.PayloadType == PayloadType.ReportSchedule && m.Value.PayloadUri == "test://payload/root/uri/blob" && m.Value.MeasureIds.Contains("TestMeasure")), It.Is<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>(h => h == null)), Times.Once());
        }

        [Fact]
        public async Task Execute_NotAllReady_PatientsToEvaluate_CallsDataAcqProducer()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var aggregator = scope.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var readyValProducer = scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
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

            // Setup submission entries - some pending evaluation
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
            schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var dataAcqKafkaMock = new Mock<IProducer<string, DataAcquisitionRequestedValue>>();
            var dataAcqProducer = new DataAcquisitionRequestedProducer(database, dataAcqKafkaMock.Object);

            var manifestProducer = new ReportManifestProducer(database, aggregator, tenantApiService, blobStorageService, submitPayloadProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();
            var triggerMock = new Mock<ITrigger>();
            var jobDataMap = new JobDataMap();
            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, schedule);
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

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
            dataAcqKafkaMock.Verify(p => p.Produce(It.Is<string>(topic => topic == nameof(KafkaTopic.DataAcquisitionRequested)),
                It.Is<Message<string, DataAcquisitionRequestedValue>>(m => m.Key == schedule.FacilityId && m.Value.PatientId == "Patient1" && m.Value.ReportableEvent == "EOM" && m.Value.ScheduledReports[0].ReportTrackingId == schedule.Id), It.Is<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>(h => h == null)), Times.Once());

            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);
        }
        [Fact]
        public async Task Execute_NotAllReady_NeedsValidation_CallsReadyForValidationProducer()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var aggregator = scope.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var dataAcqProducer = scope.ServiceProvider.GetRequiredService<DataAcquisitionRequestedProducer>();
            var tenantApiService = scope.ServiceProvider.GetRequiredService<ITenantApiService>();

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

            // Setup submission entries - needs validation
            var entry = new MeasureReportSubmissionEntryModel
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

            // Mocks
            var loggerMock = new Mock<ILogger<EndOfReportPeriodJob>>();
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            var schedulerMock = new Mock<IScheduler>();
            schedulerFactoryMock.Setup(f => f.GetScheduler(It.IsAny<CancellationToken>())).ReturnsAsync(schedulerMock.Object);
            schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var readyValKafkaMock = new Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>>();

            // Create producer with mocked Kafka producer and resolved manager
            var readyValProducer = new ReadyForValidationProducer(readyValKafkaMock.Object, scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>());

            var manifestProducer = new ReportManifestProducer(database, aggregator, tenantApiService, blobStorageService, submitPayloadProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();
            var triggerMock = new Mock<ITrigger>();
            var jobDataMap = new JobDataMap();
            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, schedule);
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

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
            readyValKafkaMock.Verify(p => p.Produce(It.Is<string>(topic => topic == nameof(KafkaTopic.ReadyForValidation)),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m => m.Key.FacilityId == schedule.FacilityId && m.Value.PatientId == "Patient1" && m.Value.ReportTrackingId == schedule.Id && m.Value.ReportTypes.Contains("TestReport") && m.Value.PayloadUri == "test://payload/patient1"), It.Is<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>(h => h == null)), Times.Once());

            var updatedSchedule = await database.ReportScheduledRepository.SingleOrDefaultAsync(s => s.Id == schedule.Id);
            Assert.Equal(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.True(updatedSchedule.EndOfReportPeriodJobHasRun);

            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.Equal(ValidationStatus.Requested, updatedEntry.ValidationStatus);
        }

        [Fact]
        public async Task Execute_Exception_ReschedulesJob()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var aggregator = scope.ServiceProvider.GetRequiredService<MeasureReportAggregator>();
            var blobStorageService = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            var submitPayloadProducer = scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>();
            var readyValProducer = scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>();
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

            var manifestProducer = new ReportManifestProducer(database, aggregator, tenantApiService, blobStorageService, submitPayloadProducer);

            // Job context
            var contextMock = new Mock<IJobExecutionContext>();
            var triggerMock = new Mock<ITrigger>();
            var jobDataMap = new JobDataMap();
            jobDataMap.Put(ReportConstants.MeasureReportSubmissionScheduler.ReportScheduleModel, schedule);
            triggerMock.Setup(t => t.JobDataMap).Returns(jobDataMap);
            contextMock.Setup(c => c.Trigger).Returns(triggerMock.Object);

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
            Assert.NotEqual(ScheduleStatus.EndOfPeriod, updatedSchedule.Status);
            Assert.False(updatedSchedule.EndOfReportPeriodJobHasRun);

            schedulerMock.Verify(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}