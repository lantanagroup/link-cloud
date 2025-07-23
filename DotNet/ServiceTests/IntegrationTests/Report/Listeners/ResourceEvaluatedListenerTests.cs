using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ResourceEvaluatedListenerTests
    {
        private readonly ReportIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ResourceEvaluatedListenerTests(ReportIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableResource_NewResource_AddsToDBUpdatesEntry()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Setup schedule and entry in DB
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                ReportType = "TestReport",
                Status = PatientSubmissionStatus.PendingEvaluation,
                PayloadUri = "test://payload/patient1",
                ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult for a reportable resource (e.g., Patient)
            var resource = new Patient
            {
                Id = "Patient1"
            };
            var resourceJson = new FhirJsonSerializer().SerializeToString(resource);
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey() { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = schedule.Id,
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse(resourceJson).RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute
            await listener.ProcessMessageAsync(consumeResult, default);

            // Asserts (using real DB to check updates)
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(PatientSubmissionStatus.PendingEvaluation, updatedEntry.Status); // Status doesn't change for non-MeasureReport resources

            var createdResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
                r.FacilityId == "TestFacility" &&
                r.PatientId == "Patient1" &&
                r.ResourceType == "Patient");
            Assert.NotNull(createdResource);
            Assert.IsType<Patient>(createdResource.GetResource());
            Assert.Equal("Patient1", ((Patient)createdResource.GetResource()).Id);

            Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "Patient" && cr.ResourceId == "Patient1" && cr.CategoryType == ResourceCategoryType.Patient);

            ReportIntegrationTestFixture.ReadyForValidationProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Value.PatientId == "Patient1" &&
                    m.Value.ReportTrackingId == schedule.Id &&
                    m.Value.ReportTypes.SequenceEqual(schedule.ReportTypes) &&
                    m.Value.PayloadUri == updatedEntry.PayloadUri),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), Times.Never());

            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Key.ReportScheduleId == schedule.Id),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Never());
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableMeasureReport_AddsUpdatesStatusGeneratesBundleProducesReadyForValidation()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            // Setup schedule and entry in DB
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                ReportType = "TestReport",
                Status = PatientSubmissionStatus.PendingEvaluation,
                PayloadUri = "test://payload/patient1",
                ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult for a reportable MeasureReport
            var measureReport = new MeasureReport
            {
                Id = "MeasureReport1",
                Measure = "TestReport",
                Status = MeasureReport.MeasureReportStatus.Complete,
                Type = MeasureReport.MeasureReportType.Individual
            };
            var measureReportJson = new FhirJsonSerializer().SerializeToString(measureReport);
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = schedule.Id,
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse(measureReportJson).RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute
            await listener.ProcessMessageAsync(consumeResult, default);

            // Asserts (using real DB to check updates)
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(PatientSubmissionStatus.ValidationRequested, updatedEntry.Status);

            Assert.NotNull(updatedEntry.MeasureReport);
            Assert.Equal("MeasureReport1", updatedEntry.MeasureReport.Id);

            var blobStorageMock = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            Mock.Get(blobStorageMock).Verify(b => b.UploadAsync(schedule, It.IsAny<PatientSubmissionModel>(), It.IsAny<CancellationToken>()), Times.Never());

            ReportIntegrationTestFixture.ReadyForValidationProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Value.PatientId == "Patient1" &&
                    m.Value.ReportTrackingId == schedule.Id &&
                    m.Value.PayloadUri == updatedEntry.PayloadUri &&
                    m.Value.ReportTypes.SequenceEqual(schedule.ReportTypes)),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), Times.Once());

            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Key.ReportScheduleId == schedule.Id),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Never());
        }

        [Fact]
        public async Task ProcessMessageAsync_NotReportable_UpdatesStatusToNotReportable()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            // Setup schedule and entry in DB
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                ReportType = "TestReport",
                Status = PatientSubmissionStatus.PendingEvaluation,
                PayloadUri = "test://payload/patient1",
                ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult for a not reportable resource (e.g., MeasureReport)
            var measureReport = new MeasureReport
            {
                Id = "MeasureReport1",
                Measure = "TestReport",
                Status = MeasureReport.MeasureReportStatus.Complete,
                Type = MeasureReport.MeasureReportType.Individual
            };
            var measureReportJson = new FhirJsonSerializer().SerializeToString(measureReport);
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = schedule.Id,
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse(measureReportJson).RootElement,
                    IsReportable = false
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute
            await listener.ProcessMessageAsync(consumeResult, default);

            // Asserts (using real DB to check updates)
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(PatientSubmissionStatus.NotReportable, updatedEntry.Status);

            Assert.NotNull(updatedEntry.MeasureReport);
            Assert.Equal("MeasureReport1", updatedEntry.MeasureReport.Id);

            var blobStorageMock = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            Mock.Get(blobStorageMock).Verify(b => b.UploadAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<PatientSubmissionModel>(), It.IsAny<CancellationToken>()), Times.Never());

            ReportIntegrationTestFixture.ReadyForValidationProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Value.PatientId == "Patient1" &&
                    m.Value.ReportTrackingId == schedule.Id &&
                    m.Value.PayloadUri == updatedEntry.PayloadUri &&
                    m.Value.ReportTypes.SequenceEqual(schedule.ReportTypes)),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), Times.Never());

            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Key.ReportScheduleId == schedule.Id),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Never());
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableResource_MergesExistingResource()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var serviceScopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            // Setup schedule and entry in DB
            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                ReportType = "TestReport",
                Status = PatientSubmissionStatus.PendingEvaluation,
                PayloadUri = "test://payload/patient1",
                ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Setup existing resource in DB
            var existingPatient = new Patient
            {
                Id = "Patient1",
                Name = new List<HumanName> { new HumanName { Family = "Old" } }
            };
            var existingResource = new PatientResourceModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = "TestFacility",
                PatientId = "Patient1",
                ResourceType = "Patient",
                ResourceId = "Patient1"
            };
            existingResource.SetResource(existingPatient);
            await database.PatientResourceRepository.AddAsync(existingResource);

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult for a reportable resource (e.g., Patient with new name)
            var newPatient = new Patient
            {
                Id = "Patient1",
                Name = new List<HumanName> { new HumanName { Family = "New" } }
            };
            var resourceJson = new FhirJsonSerializer().SerializeToString(newPatient);
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = schedule.Id,
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse(resourceJson).RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute
            await listener.ProcessMessageAsync(consumeResult, default);

            // Asserts (using real DB to check updates)
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            Assert.NotNull(updatedEntry);
            Assert.Equal(PatientSubmissionStatus.PendingEvaluation, updatedEntry.Status); // Status doesn't change for non-MeasureReport resources

            var updatedResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
                r.FacilityId == "TestFacility" &&
                r.PatientId == "Patient1" &&
                r.ResourceType == "Patient");
            Assert.NotNull(updatedResource);
            Assert.IsType<Patient>(updatedResource.GetResource());
            Assert.Equal("New", ((Patient)updatedResource.GetResource()).Name.First().Family); // Verify merged with latest strategy

            Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "Patient" && cr.ResourceId == "Patient1" && cr.CategoryType == ResourceCategoryType.Patient);

            ReportIntegrationTestFixture.ReadyForValidationProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Value.PatientId == "Patient1" &&
                    m.Value.ReportTrackingId == schedule.Id &&
                    m.Value.PayloadUri == updatedEntry.PayloadUri &&
                    m.Value.ReportTypes.SequenceEqual(schedule.ReportTypes)),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), Times.Never());

            ReportIntegrationTestFixture.SubmitPayloadProducerMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Key.ReportScheduleId == schedule.Id),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), Times.Never());
        }

        [Fact]
        public async Task ProcessMessageAsync_NoSchedule_TransientException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Create listener with real scope factory (no schedule added to DB)
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult with non-existent schedule
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = "nonexistent",
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<TransientException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("report schedule not found", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_TimeoutException_TransientException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Mock IServiceScopeFactory to override IReportScheduledManager for timeout
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.GetReportSchedule(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());

            // Mock TransientExceptionHandler
            var mockTransientHandler = new Mock<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();

            // Mock Consumer
            var mockConsumer = new Mock<IConsumer<ResourceEvaluatedKey, ResourceEvaluatedValue>>();

            // Create listener with mocked transient handler
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                mockTransientHandler.Object,
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                mockScopeFactory.Object,
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = "testid",
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute HandleConsumeResult (no exception expected)
            await listener.HandleConsumeResult(consumeResult, mockConsumer.Object, default);

            // Assert that transient handler was called with TransientException having inner TimeoutException
            mockTransientHandler.Verify(h => h.HandleException(
                consumeResult,
                It.Is<TransientException>(te => te.InnerException is TimeoutException),
                "TestFacility"
            ), Times.Once());

            // Verify consumer commit was called
            mockConsumer.Verify(c => c.Commit(consumeResult), Times.Once());
        }

        [Fact]
        public async Task ProcessMessageAsync_GeneralException_TransientException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Mock IServiceScopeFactory to override IReportScheduledManager for general exception
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.GetReportSchedule(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Test error"));

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                mockScopeFactory.Object,
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = "testid",
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<Exception>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Equal("Test error", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_InvalidResource_DeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Setup schedule and entry in DB to pass the schedule and entry checks
            var schedule = new ReportScheduleModel
            {
                Id = "testid",
                FacilityId = "TestFacility",
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = new List<string> { "TestReport" },
                Frequency = Frequency.Monthly
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entry = new MeasureReportSubmissionEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = schedule.FacilityId,
                ReportScheduleId = schedule.Id,
                PatientId = "Patient1",
                ReportType = "TestReport",
                Status = PatientSubmissionStatus.PendingEvaluation,
                PayloadUri = "test://payload/patient1",
                ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
            };
            await database.SubmissionEntryRepository.AddAsync(entry);

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult with invalid FHIR JSON (valid JSON but missing resourceType)
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = "testid",
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse("{}").RootElement,
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("Unable to deserialize event resource", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_MissingCorrelationId_DeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult without correlation ID
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = "testid",
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement,
                    IsReportable = true
                },
                Headers = new Headers()
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("Received message without correlation ID", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_MissingResource_DeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            // Create listener
            var listener = new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());

            // Simulate ConsumeResult without resource
            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = "TestFacility" },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = "testid",
                    PatientId = "Patient1",
                    ReportType = "TestReport",
                    Resource = JsonDocument.Parse("{\"dummy\":null}").RootElement.GetProperty("dummy"),
                    IsReportable = true
                },
                Headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) } }
            };
            var consumeResult = new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };

            // Execute and assert
            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("Received message without a value in the resource property", exception.Message);
        }
    }
}