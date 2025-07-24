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

        private ResourceEvaluatedListener CreateListener(IServiceScope scope)
        {
            return new ResourceEvaluatedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>());
        }

        private async Task<(ReportScheduleModel schedule, List<MeasureReportSubmissionEntryModel> entries)> SetupDatabaseAsync(IServiceScope scope, string facilityId = "TestFacility", List<string> reportTypes = null, List<(string patientId, string reportType, PatientSubmissionStatus status)> entryData = null, List<(string resourceType, string resourceId, DomainResource resource)> existingResources = null)
        {
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            reportTypes ??= new List<string> { "TestReport" };
            entryData ??= new List<(string, string, PatientSubmissionStatus)> { ("Patient1", "TestReport", PatientSubmissionStatus.PendingEvaluation) };

            var schedule = new ReportScheduleModel
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow,
                ReportTypes = reportTypes,
                Frequency = Frequency.Monthly
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entries = new List<MeasureReportSubmissionEntryModel>();
            foreach (var (patientId, reportType, status) in entryData)
            {
                var entry = new MeasureReportSubmissionEntryModel
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = schedule.FacilityId,
                    ReportScheduleId = schedule.Id,
                    PatientId = patientId,
                    ReportType = reportType,
                    Status = status,
                    PayloadUri = $"test://payload/{patientId}",
                    ContainedResources = new List<MeasureReportSubmissionEntryModel.ContainedResource>()
                };
                await database.SubmissionEntryRepository.AddAsync(entry);
                entries.Add(entry);
            }

            if (existingResources != null)
            {
                foreach (var (resourceType, resourceId, resource) in existingResources)
                {
                    var patientResource = new PatientResourceModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        FacilityId = facilityId,
                        PatientId = "Patient1",
                        ResourceType = resourceType,
                        ResourceId = resourceId
                    };
                    patientResource.SetResource(resource);
                    await database.PatientResourceRepository.AddAsync(patientResource);
                }
            }

            return (schedule, entries);
        }

        private ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> CreateConsumeResult(string facilityId, string reportTrackingId, string patientId, string reportType, JsonElement resourceElement, bool isReportable, bool hasCorrelationId = true)
        {
            var headers = new Headers();
            if (hasCorrelationId)
            {
                headers.Add("X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }

            var message = new Message<ResourceEvaluatedKey, ResourceEvaluatedValue>
            {
                Key = new ResourceEvaluatedKey { FacilityId = facilityId },
                Value = new ResourceEvaluatedValue
                {
                    ReportTrackingId = reportTrackingId,
                    PatientId = patientId,
                    ReportType = reportType,
                    Resource = resourceElement,
                    IsReportable = isReportable
                },
                Headers = headers
            };
            return new ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue> { Message = message, Topic = nameof(KafkaTopic.ResourceEvaluated) };
        }

        private JsonElement CreateResourceJson(DomainResource resource)
        {
            var json = new FhirJsonSerializer().SerializeToString(resource);
            return JsonDocument.Parse(json).RootElement;
        }

        private void AssertEntryStatusAndMeasureReport(MeasureReportSubmissionEntryModel updatedEntry, PatientSubmissionStatus expectedStatus, string expectedMeasureReportId = null)
        {
            Assert.NotNull(updatedEntry);
            Assert.Equal(expectedStatus, updatedEntry.Status);

            if (expectedMeasureReportId != null)
            {
                Assert.NotNull(updatedEntry.MeasureReport);
                Assert.Equal(expectedMeasureReportId, updatedEntry.MeasureReport.Id);
            }
        }

        private void AssertProducerMocks(Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>> readyMock, Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> submitMock, Times readyTimes, Times submitTimes, ReportScheduleModel schedule, MeasureReportSubmissionEntryModel entry)
        {
            readyMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Value.PatientId == entry.PatientId &&
                    m.Value.ReportTrackingId == schedule.Id &&
                    m.Value.ReportTypes.SequenceEqual(schedule.ReportTypes) &&
                    m.Value.PayloadUri == entry.PayloadUri),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), readyTimes);

            submitMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == "TestFacility" &&
                    m.Key.ReportScheduleId == schedule.Id),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), submitTimes);
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableResource_NewResource_AddsToDBUpdatesEntry()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var (schedule, entries) = await SetupDatabaseAsync(scope);
            var entry = entries.First();

            var listener = CreateListener(scope);

            var patient = new Patient { Id = "Patient1" };
            var consumeResult = CreateConsumeResult("TestFacility", schedule.Id, "Patient1", "TestReport", CreateResourceJson(patient), true);

            await listener.ProcessMessageAsync(consumeResult, default);

            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            AssertEntryStatusAndMeasureReport(updatedEntry, PatientSubmissionStatus.PendingEvaluation);

            var createdResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
                r.FacilityId == "TestFacility" && r.PatientId == "Patient1" && r.ResourceType == "Patient");
            Assert.NotNull(createdResource);
            Assert.IsType<Patient>(createdResource.GetResource());
            Assert.Equal("Patient1", ((Patient)createdResource.GetResource()).Id);

            Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "Patient" && cr.ResourceId == "Patient1" && cr.CategoryType == ResourceCategoryType.Patient);

            AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Never(), Times.Never(), schedule, updatedEntry);
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableMeasureReport_ProducesReadyForValidation()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var (schedule, entries) = await SetupDatabaseAsync(scope);
            var entry = entries.First();

            var listener = CreateListener(scope);

            var measureReport = new MeasureReport
            {
                Id = "MeasureReport1",
                Measure = "TestReport",
                Status = MeasureReport.MeasureReportStatus.Complete,
                Type = MeasureReport.MeasureReportType.Individual
            };
            var consumeResult = CreateConsumeResult("TestFacility", schedule.Id, "Patient1", "TestReport", CreateResourceJson(measureReport), true);

            await listener.ProcessMessageAsync(consumeResult, default);

            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            AssertEntryStatusAndMeasureReport(updatedEntry, PatientSubmissionStatus.ValidationRequested, "MeasureReport1");

            var blobStorageMock = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            Mock.Get(blobStorageMock).Verify(b => b.UploadAsync(schedule, It.IsAny<PatientSubmissionModel>(), It.IsAny<CancellationToken>()), Times.Never());

            AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Once(), Times.Never(), schedule, updatedEntry);
        }

        [Fact]
        public async Task ProcessMessageAsync_NotReportable_UpdatesStatusToNotReportable()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var entryData = new List<(string, string, PatientSubmissionStatus)>
            {
                ("Patient1", "TestReport", PatientSubmissionStatus.PendingEvaluation),
                ("Patient1", "OtherReport", PatientSubmissionStatus.PendingEvaluation)
            };
            var (schedule, entries) = await SetupDatabaseAsync(scope, reportTypes: new List<string> { "TestReport", "OtherReport" }, entryData: entryData);
            var entry = entries.First(e => e.ReportType == "TestReport");

            var listener = CreateListener(scope);

            var measureReport = new MeasureReport
            {
                Id = "MeasureReport1",
                Measure = "TestReport",
                Status = MeasureReport.MeasureReportStatus.Complete,
                Type = MeasureReport.MeasureReportType.Individual
            };
            var consumeResult = CreateConsumeResult("TestFacility", schedule.Id, "Patient1", "TestReport", CreateResourceJson(measureReport), false);

            await listener.ProcessMessageAsync(consumeResult, default);

            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            AssertEntryStatusAndMeasureReport(updatedEntry, PatientSubmissionStatus.NotReportable, "MeasureReport1");

            var blobStorageMock = scope.ServiceProvider.GetRequiredService<BlobStorageService>();
            Mock.Get(blobStorageMock).Verify(b => b.UploadAsync(It.IsAny<ReportScheduleModel>(), It.IsAny<PatientSubmissionModel>(), It.IsAny<CancellationToken>()), Times.Never());

            AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Never(), Times.Never(), schedule, updatedEntry);
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableResource_MergesExistingResource()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var existingResources = new List<(string, string, DomainResource)>
            {
                ("Patient", "Patient1", new Patient { Id = "Patient1", Name = { new HumanName { Family = "Old" } } })
            };
            var (schedule, entries) = await SetupDatabaseAsync(scope, existingResources: existingResources);
            var entry = entries.First();

            var listener = CreateListener(scope);

            var newPatient = new Patient { Id = "Patient1", Name = { new HumanName { Family = "New" } } };
            var consumeResult = CreateConsumeResult("TestFacility", schedule.Id, "Patient1", "TestReport", CreateResourceJson(newPatient), true);

            await listener.ProcessMessageAsync(consumeResult, default);

            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            AssertEntryStatusAndMeasureReport(updatedEntry, PatientSubmissionStatus.PendingEvaluation);

            var updatedResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
                r.FacilityId == "TestFacility" && r.PatientId == "Patient1" && r.ResourceType == "Patient");
            Assert.NotNull(updatedResource);
            Assert.IsType<Patient>(updatedResource.GetResource());
            Assert.Equal("New", ((Patient)updatedResource.GetResource()).Name.First().Family);

            Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "Patient" && cr.ResourceId == "Patient1" && cr.CategoryType == ResourceCategoryType.Patient);

            AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Never(), Times.Never(), schedule, updatedEntry);
        }

        [Fact]
        public async Task ProcessMessageAsync_NoSchedule_TransientException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var listener = CreateListener(scope);

            var consumeResult = CreateConsumeResult("TestFacility", "nonexistent", "Patient1", "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true);

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

            var consumeResult = CreateConsumeResult("TestFacility", "testid", "Patient1", "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true);

            var exception = await Assert.ThrowsAsync<Exception>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Equal("Test error", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_InvalidResource_DeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();
            var (schedule, _) = await SetupDatabaseAsync(scope, reportTypes: new List<string> { "TestReport" });

            var listener = CreateListener(scope);

            var consumeResult = CreateConsumeResult("TestFacility", schedule.Id, "Patient1", "TestReport", JsonDocument.Parse("{}").RootElement, true);

            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("Unable to deserialize event resource", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_MissingCorrelationId_DeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var listener = CreateListener(scope);

            var consumeResult = CreateConsumeResult("TestFacility", "testid", "Patient1", "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true, hasCorrelationId: false);

            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("Received message without correlation ID", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_MissingResource_DeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var listener = CreateListener(scope);

            var consumeResult = CreateConsumeResult("TestFacility", "testid", "Patient1", "TestReport", JsonDocument.Parse("null").RootElement, true);

            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("Received message without a value in the resource property", exception.Message);
        }
    }
}