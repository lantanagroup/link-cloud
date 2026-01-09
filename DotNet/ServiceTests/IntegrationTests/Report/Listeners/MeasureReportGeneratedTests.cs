using Azure.Storage.Blobs;
using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Domain.Managers;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Report.Settings;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class MeasureReportGeneratedTests
    {
        private readonly ReportIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public MeasureReportGeneratedTests(ReportIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private MeasureReportGeneratedListener CreateListener(IServiceScope scope)
        {
            return new MeasureReportGeneratedListener(
                scope.ServiceProvider.GetRequiredService<ILogger<MeasureReportGeneratedListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<Null, MeasureReportGeneratedValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<Null, MeasureReportGeneratedValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<Null, MeasureReportGeneratedValue>>(),
                scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<PatientAggregator>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>(),
                scope.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>(),
                scope.ServiceProvider.GetRequiredService<IReportEntryManager>(),
                scope.ServiceProvider.GetRequiredService<IReportScheduledManager>(),
                scope.ServiceProvider.GetRequiredService<IReportPopulationManager>(),
                scope.ServiceProvider.GetRequiredService<IReportResourceManager>());
        }

        private async Task<(ReportScheduleModel schedule, List<ReportEntryModel> entries)> SetupDatabaseAsync(IServiceScope scope, string facilityId, List<string> reportTypes = null, List<(string patientId, string reportType, MeasureReportStatus status)> entryData = null, List<(string resourceType, string resourceId, DomainResource resource)> existingResources = null)
        {
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            reportTypes ??= new List<string> { "TestReport" };

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

            var entries = new List<ReportEntryModel>();
            foreach (var (patientId, reportType, status) in entryData)
            {
                var entry = new ReportEntryModel()
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = schedule.FacilityId,
                    ReportScheduleId = schedule.Id,
                    PatientId = patientId,
                    AggregateReportUri = $"test://payload/{patientId}",
                    AggregateReportBlobName = $"payload/{patientId}",
                };

                await database.ReportEntryRepository.AddAsync(entry);
                entries.Add(entry);
            }

            //TODO: Add MeasureReport List to entry?

            return (schedule, entries);
        }

        private ConsumeResult<Null, MeasureReportGeneratedValue> CreateConsumeResult(string facilityId, string reportTrackingId, string patientId, string reportType, string measureReportId, string measureReportURI, string measureReportFileName, bool isReportable, bool hasCorrelationId = true)
        {
            var headers = new Headers();
            if (hasCorrelationId)
            {
                headers.Add("X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }

            var message = new Message<Null, MeasureReportGeneratedValue>
            {
                Value = new MeasureReportGeneratedValue
                {
                    FacilityId = facilityId,
                    ReportTrackingId = reportTrackingId,
                    PatientId = patientId,
                    ReportType = reportType,
                    MeasureReportId = measureReportId,
                    MeasureReportURI = measureReportURI,
                    MeasureReportFileName = measureReportFileName,
                    IsReportable = isReportable
                },
                Headers = headers
            };
            return new ConsumeResult<Null, MeasureReportGeneratedValue> { Message = message, Topic = nameof(KafkaTopic.MeasureReportGenerated) };
        }

        private void AssertEntryStatusAndMeasureReport(ReportEntryModel updatedEntry, ReportingStatus expectedStatus, string expectedMeasureReportId = null)
        {
            Assert.NotNull(updatedEntry);
            Assert.Equal(expectedStatus, updatedEntry.ReportingStatus);

            if (expectedMeasureReportId != null)
            {
                //Assert.NotNull(updatedEntry.MeasureReport);
                Assert.Equal(expectedMeasureReportId, updatedEntry.MeasureReportList.Where(x => x.MeasureReportId == x.MeasureReportId).First().MeasureReportId);
            }
        }

        private void AssertProducerMocks(Mock<IProducer<ReadyForValidationKey, ReadyForValidationValue>> readyMock, Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> submitMock, Times readyTimes, Times submitTimes, ReportScheduleModel schedule, ReportEntryModel entry)
        {
            readyMock.Verify(p => p.Produce(
                nameof(KafkaTopic.ReadyForValidation),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Value.PatientId == entry.PatientId &&
                    m.Value.ReportTrackingId == schedule.Id &&
                    m.Value.ReportTypes.SequenceEqual(schedule.ReportTypes) &&
                    m.Value.PayloadUri == entry.AggregateReportUri),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()), readyTimes);

            submitMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), submitTimes);
        }

        private async Task AssertNoBlobUploaded(IServiceScope scope, ReportScheduleModel schedule, ReportEntryModel entry)
        {
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<BlobStorageSettings>>().Value;
            var containerClient = new BlobContainerClient(settings.ConnectionString, settings.BlobContainerName);

            // Use the same GetReportName method as AssertBlobUploaded
            var reportName = ReportHelpers.GetReportName(schedule.Id, schedule.FacilityId, schedule.ReportTypes, schedule.ReportStartDate);
            var bundleName = $"patient-{entry.PatientId}.ndjson";
            var blobName = $"{reportName}/{bundleName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            bool exists = await blobClient.ExistsAsync();
            Assert.False(exists);
        }

        private async Task AssertBlobUploaded(IServiceScope scope, ReportScheduleModel schedule, ReportEntryModel entry)
        {
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<BlobStorageSettings>>().Value;
            var containerClient = new BlobContainerClient(settings.ConnectionString, settings.BlobContainerName);

            var reportName = ReportHelpers.GetReportName(schedule.Id, schedule.FacilityId, schedule.ReportTypes, schedule.ReportStartDate);
            var bundleName = $"patient-{entry.PatientId}.ndjson";
            var blobName = $"{reportName}/{bundleName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            bool exists = await blobClient.ExistsAsync();
            Assert.True(exists);
        }

        [Fact]
        public async Task ProcessMessageAsync_ReportableResource_NewResource_AddsToDBUpdatesEntry()
        {
            var facilityId = Guid.NewGuid().ToString();
            var patientId = Guid.NewGuid().ToString();

            using var scope = _fixture.ServiceProvider.CreateScope();
            var (schedule, entries) = await SetupDatabaseAsync(scope, facilityId, entryData: new List<(string patientId, string reportType, MeasureReportStatus status)> { (patientId, "TestReport", MeasureReportStatus.PendingEvaluation) });
            var entry = entries.First();

            var listener = CreateListener(scope);

            var measureReportId = Guid.NewGuid().ToString();
            var blobName = patientId + ".mr";
            var blobURI = "test:/devstoreaccount1/internal/" + blobName;

            var consumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, "TestReport", measureReportId, blobURI, blobName, true);

            await listener.ProcessMessageAsync(consumeResult, facilityId, default);

            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
            var updatedEntry = await database.ReportEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);

            AssertEntryStatusAndMeasureReport(updatedEntry, ReportingStatus.PatientIdentified);

            //var createdResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
            //    r.FacilityId == facilityId && r.PatientId == patientId && r.ResourceType == "Patient");
            //Assert.NotNull(createdResource);
            //Assert.IsType<Patient>(createdResource.GetResource());
            //Assert.Equal(patientId, ((Patient)createdResource.GetResource()).Id);

            //Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "Patient" && cr.ResourceId == patientId && cr.CategoryType == ResourceCategoryType.Patient);

            AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Never(), Times.Never(), schedule, updatedEntry);

            await AssertNoBlobUploaded(scope, schedule, updatedEntry);
        }

        //[Fact]
        //public async Task ProcessMessageAsync_ReportableMeasureReport_ProducesReadyForValidation()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var (schedule, entries) = await SetupDatabaseAsync(scope, facilityId, entryData: new List<(string patientId, string reportType, MeasureReportStatus status)> { (patientId, "TestReport", MeasureReportStatus.PendingEvaluation) });
        //    var entry = entries.First();

        //    var listener = CreateListener(scope);

        //    var measureReport = new MeasureReport
        //    {
        //        Id = "MeasureReport1",
        //        Measure = "TestReport",
        //        Status = MeasureReport.MeasureReportStatus.Complete,
        //        Type = MeasureReport.MeasureReportType.Individual,
        //        Period = new Period { Start = "2024-01-01", End = "2024-01-31" }
        //    };
        //    var consumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, "TestReport", CreateResourceJson(measureReport), true);

        //    await listener.ProcessMessageAsync(consumeResult, default);

        //    var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        //    var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
        //    AssertEntryStatusAndMeasureReport(updatedEntry, MeasureReportStatus.ValidationRequested, "MeasureReport1");

        //    AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Once(), Times.Never(), schedule, updatedEntry);

        //    await AssertBlobUploaded(scope, schedule, updatedEntry);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_NotReportable_UpdatesStatusToNotReportable()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var entryData = new List<(string, string, MeasureReportStatus)>
        //    {
        //        (patientId, "TestReport", MeasureReportStatus.PendingEvaluation),
        //        (patientId, "OtherReport", MeasureReportStatus.PendingEvaluation)
        //    };
        //    var (schedule, entries) = await SetupDatabaseAsync(scope, facilityId, reportTypes: new List<string> { "TestReport", "OtherReport" }, entryData: entryData);
        //    var entry = entries.First(e => e.ReportType == "TestReport");

        //    var listener = CreateListener(scope);

        //    var measureReport = new MeasureReport
        //    {
        //        Id = "MeasureReport1",
        //        Measure = "TestReport",
        //        Status = MeasureReport.MeasureReportStatus.Complete,
        //        Type = MeasureReport.MeasureReportType.Individual,
        //        Period = new Period { Start = "2024-01-01", End = "2024-01-31" }
        //    };
        //    var consumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, "TestReport", CreateResourceJson(measureReport), false);

        //    await listener.ProcessMessageAsync(consumeResult, default);

        //    var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        //    var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
        //    AssertEntryStatusAndMeasureReport(updatedEntry, MeasureReportStatus.NotReportable, "MeasureReport1");

        //    AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Never(), Times.Never(), schedule, updatedEntry);

        //    await AssertNoBlobUploaded(scope, schedule, updatedEntry);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_ReportableResource_MergesExistingResource()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var existingResources = new List<(string, string, DomainResource)>
        //    {
        //        ("Patient", patientId, new Patient { Id = patientId, Name = { new HumanName { Family = "Old" } } })
        //    };
        //    var (schedule, entries) = await SetupDatabaseAsync(scope, facilityId, entryData: new List<(string patientId, string reportType, MeasureReportStatus status)> { (patientId, "TestReport", MeasureReportStatus.PendingEvaluation) }, existingResources: existingResources);
        //    var entry = entries.First();

        //    var listener = CreateListener(scope);

        //    var newPatient = new Patient { Id = patientId, Name = { new HumanName { Family = "New" } } };
        //    var consumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, "TestReport", CreateResourceJson(newPatient), true);

        //    await listener.ProcessMessageAsync(consumeResult, default);

        //    var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        //    var updatedEntry = await database.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
        //    AssertEntryStatusAndMeasureReport(updatedEntry, MeasureReportStatus.PendingEvaluation);

        //    var updatedResource = await database.PatientResourceRepository.FirstOrDefaultAsync(r =>
        //        r.FacilityId == facilityId && r.PatientId == patientId && r.ResourceType == "Patient");
        //    Assert.NotNull(updatedResource);
        //    Assert.IsType<Patient>(updatedResource.GetResource());
        //    Assert.Equal("New", ((Patient)updatedResource.GetResource()).Name.First().Family);

        //    Assert.Contains(updatedEntry.ContainedResources, cr => cr.ResourceType == "Patient" && cr.ResourceId == patientId && cr.CategoryType == ResourceCategoryType.Patient);

        //    AssertProducerMocks(ReportIntegrationTestFixture.ReadyForValidationProducerMock, ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Never(), Times.Never(), schedule, updatedEntry);

        //    await AssertNoBlobUploaded(scope, schedule, updatedEntry);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_NoSchedule_TransientException()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var listener = CreateListener(scope);

        //    var consumeResult = CreateConsumeResult(facilityId, "nonexistent", patientId, "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true);

        //    var exception = await Assert.ThrowsAsync<TransientException>(() => listener.ProcessMessageAsync(consumeResult, default));
        //    Assert.Contains("report schedule not found", exception.Message);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_TimeoutException_TransientException()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

        //    var mockScopeFactory = new Mock<IServiceScopeFactory>();
        //    var mockScope = new Mock<IServiceScope>();
        //    var mockServiceProvider = new Mock<IServiceProvider>();
        //    mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        //    mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
        //    mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
        //    mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        //    var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
        //    Mock.Get(reportScheduledManagerMock).Setup(m => m.GetReportSchedule(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());

        //    var listener = new ResourceEvaluatedListener(
        //        scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
        //        scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
        //        scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
        //        scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
        //        mockScopeFactory.Object,
        //        scope.ServiceProvider.GetRequiredService<PatientAggregator>(),
        //        scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
        //        scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
        //        scope.ServiceProvider.GetRequiredService<ReportManifestProducer>(),
        //        scope.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>());

        //    var consumeResult = CreateConsumeResult(facilityId, "testid", patientId, "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true);

        //    var consumerConfig = new ConsumerConfig()
        //    {
        //        GroupId = ReportConstants.ServiceName,
        //        EnableAutoCommit = false
        //    };

        //    var kafkaConsumerFactory = scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
        //    using var consumer = kafkaConsumerFactory.CreateConsumer(consumerConfig);
        //    await listener.HandleConsumeResult(consumeResult, consumer, default);

        //    var transientHandlerMock = scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
        //    Mock.Get(transientHandlerMock).Verify(h => h.HandleException(It.IsAny<ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue>>(), It.IsAny<TransientException>(), It.IsAny<string>()), Times.Once);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_GeneralException_TransientException()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

        //    var mockScopeFactory = new Mock<IServiceScopeFactory>();
        //    var mockScope = new Mock<IServiceScope>();
        //    var mockServiceProvider = new Mock<IServiceProvider>();
        //    mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
        //    mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
        //    mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
        //    mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        //    var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
        //    Mock.Get(reportScheduledManagerMock).Setup(m => m.GetReportSchedule(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Test error"));

        //    var listener = new ResourceEvaluatedListener(
        //        scope.ServiceProvider.GetRequiredService<ILogger<ResourceEvaluatedListener>>(),
        //        scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
        //        scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
        //        scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>(),
        //        mockScopeFactory.Object,
        //        scope.ServiceProvider.GetRequiredService<PatientAggregator>(),
        //        scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
        //        scope.ServiceProvider.GetRequiredService<ReadyForValidationProducer>(),
        //        scope.ServiceProvider.GetRequiredService<ReportManifestProducer>(),
        //        scope.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>());

        //    var consumeResult = CreateConsumeResult(facilityId, "testid", patientId, "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true);

        //    var consumerConfig = new ConsumerConfig()
        //    {
        //        GroupId = ReportConstants.ServiceName,
        //        EnableAutoCommit = false
        //    };

        //    var kafkaConsumerFactory = scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
        //    using var consumer = kafkaConsumerFactory.CreateConsumer(consumerConfig);
        //    await listener.HandleConsumeResult(consumeResult, consumer, default);

        //    var transientHandlerMock = scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<ResourceEvaluatedKey, ResourceEvaluatedValue>>();
        //    Mock.Get(transientHandlerMock).Verify(h => h.HandleException(It.IsAny<ConsumeResult<ResourceEvaluatedKey, ResourceEvaluatedValue>>(), It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_InvalidResource_DeadLetterException()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();
        //    var (schedule, _) = await SetupDatabaseAsync(scope, facilityId, entryData: new List<(string patientId, string reportType, MeasureReportStatus status)> { (patientId, "TestReport", MeasureReportStatus.PendingEvaluation) });

        //    var listener = CreateListener(scope);

        //    var consumeResult = CreateConsumeResult(facilityId, schedule.Id, patientId, "TestReport", JsonDocument.Parse("{}").RootElement, true);

        //    var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
        //    Assert.Contains("Unable to deserialize event resource", exception.Message);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_MissingCorrelationId_DeadLetterException()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();

        //    var listener = CreateListener(scope);

        //    var consumeResult = CreateConsumeResult(facilityId, "testid", patientId, "TestReport", JsonDocument.Parse("{\"resourceType\": \"Patient\"}").RootElement, true, hasCorrelationId: false);

        //    var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
        //    Assert.Contains("Received message without correlation ID", exception.Message);
        //}

        //[Fact]
        //public async Task ProcessMessageAsync_MissingResource_DeadLetterException()
        //{
        //    var facilityId = Guid.NewGuid().ToString();
        //    var patientId = Guid.NewGuid().ToString();

        //    using var scope = _fixture.ServiceProvider.CreateScope();

        //    var listener = CreateListener(scope);

        //    var consumeResult = CreateConsumeResult(facilityId, "testid", patientId, "TestReport", JsonDocument.Parse("null").RootElement, true);

        //    var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
        //    Assert.Contains("Received message without a value in the resource property", exception.Message);
        //}
    }
}