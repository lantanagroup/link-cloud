using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Queries;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Entities.Enums;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using System.Text;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    [Trait("Category", "IntegrationTests")]
    public class ValidationCompleteListenerTests
    {
        private readonly ReportIntegrationTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public ValidationCompleteListenerTests(ReportIntegrationTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private ValidationCompleteListener CreateListener(IServiceScope scope, Mock<IServiceScopeFactory> mockScopeFactory = null)
        {
            return new ValidationCompleteListener(
                scope.ServiceProvider.GetRequiredService<ILogger<ValidationCompleteListener>>(),
                scope.ServiceProvider.GetRequiredService<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<ITransientExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<IDeadLetterExceptionHandler<string, ValidationCompleteValue>>(),
                scope.ServiceProvider.GetRequiredService<SubmitPayloadProducer>(),
                mockScopeFactory?.Object ?? scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
                scope.ServiceProvider.GetRequiredService<BlobStorageService>(),
                scope.ServiceProvider.GetRequiredService<PatientReportSubmissionBundler>(),
                scope.ServiceProvider.GetRequiredService<ReportManifestProducer>(),
                scope.ServiceProvider.GetRequiredService<AuditableEventOccurredProducer>());
        }

        private async Task<(ReportSchedule schedule, List<PatientSubmissionEntry> entries)> SetupDatabaseAsync(IServiceScope scope, string facilityId = "TestFacility", List<string> reportTypes = null, List<(string patientId, string reportType, PatientSubmissionStatus status, MeasureReport measureReport)> entryData = null)
        {
            var database = scope.ServiceProvider.GetRequiredService<IDatabase>();

            reportTypes ??= new List<string> { "TestReport" };
            entryData ??= new List<(string, string, PatientSubmissionStatus, MeasureReport)> { ("Patient1", "TestReport", PatientSubmissionStatus.ValidationRequested, null) };

            var reportStartDate = DateTime.Parse("2024-01-01").ToUniversalTime();
            var reportEndDate = DateTime.Parse("2024-01-31").ToUniversalTime();

            var schedule = new ReportSchedule
            {
                Id = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                ReportStartDate = reportStartDate,
                ReportEndDate = reportEndDate,
                ReportTypes = reportTypes,
                Frequency = Frequency.Monthly,
                PayloadRootUri = "test://payload/root"
            };
            await database.ReportScheduledRepository.AddAsync(schedule);

            var entries = new List<PatientSubmissionEntry>();
            foreach (var (patientId, reportType, status, measureReport) in entryData)
            {
                var entry = new PatientSubmissionEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    FacilityId = schedule.FacilityId,
                    ReportScheduleId = schedule.Id,
                    PatientId = patientId,
                    ReportType = reportType,
                    Status = status,
                    PayloadUri = $"test://payload/{patientId}"
                };
                if (measureReport != null)
                {
                    entry.MeasureReport = measureReport;
                }
                await database.SubmissionEntryRepository.AddAsync(entry);
                entries.Add(entry);
            }

            await database.SaveChangesAsync();

            return (schedule, entries);
        }

        private ConsumeResult<string, ValidationCompleteValue> CreateConsumeResult(string facilityId, string reportTrackingId, string patientId, bool isValid, bool hasCorrelationId = true)
        {
            var headers = new Headers();
            if (hasCorrelationId)
            {
                headers.Add("X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
            }

            var message = new Message<string, ValidationCompleteValue>
            {
                Key = facilityId,
                Value = new ValidationCompleteValue { ReportTrackingId = reportTrackingId, PatientId = patientId, IsValid = isValid },
                Headers = headers
            };
            return new ConsumeResult<string, ValidationCompleteValue> { Message = message, Topic = nameof(KafkaTopic.ValidationComplete) };
        }

        private void AssertEntryStatusAndValidation(PatientSubmissionEntry updatedEntry, PatientSubmissionStatus expectedStatus, ValidationStatus expectedValidationStatus, string expectedPayloadUri = null)
        {
            Assert.NotNull(updatedEntry);
            Assert.Equal(expectedStatus, updatedEntry.Status);
            Assert.Equal(expectedValidationStatus, updatedEntry.ValidationStatus);
            if (expectedPayloadUri != null)
            {
                Assert.Equal(expectedPayloadUri, updatedEntry.PayloadUri);
            }
        }

        private void AssertProducerMocks(Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> submitMock, Times timesEntry, Times timesSchedule, ReportSchedule schedule, string patientId, string payloadUri)
        {
            submitMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id &&
                    m.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry &&
                    m.Value.PatientId == patientId &&
                    m.Value.PayloadUri == payloadUri),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), timesEntry);

            submitMock.Verify(p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                    m.Key.FacilityId == schedule.FacilityId &&
                    m.Key.ReportScheduleId == schedule.Id &&
                    m.Value.PayloadType == PayloadType.ReportSchedule &&
                    m.Value.PayloadUri != null &&
                    m.Value.PayloadUri.EndsWith("manifest.ndjson")),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()), timesSchedule);
        }

        [Fact]
        public async Task ProcessMessageAsync_ValidValidation_UpdatesStatusAndProducesSubmitPayload()
        {
            _fixture.ResetMocks();
            //await _fixture.ClearDatabaseAsync();

            using var scope = _fixture.ServiceProvider.CreateScope();

            ReportIntegrationTestFixture.TenantApiServiceMock.Setup(t => t.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityName = "TestFacilityName" });

            var expectedManifestUri = new Uri("http://example.com/manifest.ndjson");
            ReportIntegrationTestFixture.BlobStorageMock.Setup(b => b.UploadManifestAsync(It.IsAny<ReportSchedule>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedManifestUri);

            var (schedule, entries) = await SetupDatabaseAsync(scope);
            var entry = entries.First();

            var listener = CreateListener(scope);

            var consumeResult = CreateConsumeResult(schedule.FacilityId, schedule.Id, entry.PatientId, true);

            await listener.ProcessMessageAsync(consumeResult, default);

            using var assertScope = _fixture.ServiceProvider.CreateScope();
            var assertDatabase = assertScope.ServiceProvider.GetRequiredService<IDatabase>();

            var updatedEntry = await assertDatabase.SubmissionEntryRepository.FirstOrDefaultAsync(e => e.Id == entry.Id);
            AssertEntryStatusAndValidation(updatedEntry, PatientSubmissionStatus.ValidationComplete, ValidationStatus.Passed);

            AssertProducerMocks(ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Once(), Times.Once(), schedule, entry.PatientId, updatedEntry.PayloadUri);
        }


        [Fact]
        public async Task ProcessMessageAsync_InvalidValidation_AddsOutcomeUpdatesBlobAndProducesSubmitPayload()
        {
            _fixture.ResetMocks();
            //await _fixture.ClearDatabaseAsync();

            using var scope = _fixture.ServiceProvider.CreateScope();

            // NEW: Setup TenantApiServiceMock
            ReportIntegrationTestFixture.TenantApiServiceMock.Setup(t => t.GetFacilityConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FacilityModel { FacilityName = "TestFacilityName" });

            var measureReport = new MeasureReport
            {
                Id = Guid.NewGuid().ToString(),
                Measure = "TestMeasure",
                Status = MeasureReport.MeasureReportStatus.Complete,
                Type = MeasureReport.MeasureReportType.Individual,
                Period = new Period { Start = "2024-01-01", End = "2024-01-31" }  // Added to fix null period error
            };

            var entryData = new List<(string, string, PatientSubmissionStatus, MeasureReport)>
            {
                ("Patient1", "TestReport", PatientSubmissionStatus.ValidationRequested, measureReport)
            };

            var (schedule, entries) = await SetupDatabaseAsync(scope, entryData: entryData);

            var entry = entries.First();

            var reportName = ReportHelpers.GetReportName(schedule.Id, schedule.FacilityId, schedule.ReportTypes, schedule.ReportStartDate);
            var bundleName = $"patient-{entry.PatientId}.ndjson";
            var blobName = $"{reportName}/{bundleName}";

            // Parse the BlobEndpoint from the connection string
            string connectionString = _fixture.AzuriteConnectionString;
            var parts = connectionString.Split(';');
            var blobEndpointPart = parts.FirstOrDefault(p => p.StartsWith("BlobEndpoint="));
            var blobEndpoint = blobEndpointPart?.Substring("BlobEndpoint=".Length);
            var expectedUri = $"{blobEndpoint}/report-test-container/{blobName}";
            var expectedManifestUri = $"{blobEndpoint}/report-test-container/{reportName}/manifest.ndjson";  // NEW

            ReportIntegrationTestFixture.BlobStorageMock.Setup(b => b.UploadAsync(It.IsAny<ReportSchedule>(), It.IsAny<PatientSubmissionModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri(expectedUri));
            ReportIntegrationTestFixture.BlobStorageMock.Setup(b => b.UploadManifestAsync(It.IsAny<ReportSchedule>(), It.IsAny<IEnumerable<Resource>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Uri(expectedManifestUri));

            var listener = CreateListener(scope);

            var consumeResult = CreateConsumeResult(schedule.FacilityId, schedule.Id, entry.PatientId, false);

            await listener.ProcessMessageAsync(consumeResult, default);

            // NEW: Fresh assert scope
            using var assertScope = _fixture.ServiceProvider.CreateScope();
            var assertDatabase = assertScope.ServiceProvider.GetRequiredService<IDatabase>();
            var assertQueries = assertScope.ServiceProvider.GetRequiredService<ISubmissionEntryQueries>();

            var updatedData = await assertQueries.GetPatientReportData(schedule.FacilityId, schedule.Id, entry.PatientId, cancellationToken: CancellationToken.None);
            var updatedEntry = updatedData.ReportData[schedule.ReportTypes[0]].Entries.First();

            AssertEntryStatusAndValidation(updatedEntry, PatientSubmissionStatus.ValidationComplete, ValidationStatus.Failed, expectedUri);

            Assert.Contains(updatedData.ReportData[schedule.ReportTypes[0]].Resources, cr => cr.ResourceType == "OperationOutcome");

            var createdResource = await assertDatabase.ResourceRepository.FirstOrDefaultAsync(r =>
                r.FacilityId == schedule.FacilityId && r.PatientId == entry.PatientId && r.ResourceType == "OperationOutcome");
            Assert.NotNull(createdResource);
            Assert.IsType<OperationOutcome>(createdResource.Resource);
            Assert.Equal("Patient has failed Validation", ((OperationOutcome)createdResource.Resource).Issue.First().Diagnostics);

            AssertProducerMocks(ReportIntegrationTestFixture.SubmitPayloadProducerMock, Times.Once(), Times.Once(), schedule, entry.PatientId, expectedUri);
        }

        [Fact]
        public async Task ProcessMessageAsync_NoScheduleFound_ThrowsDeadLetterException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<ReportSchedule, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync((ReportSchedule)null);

            var listener = CreateListener(scope, mockScopeFactory);

            var consumeResult = CreateConsumeResult("TestFacility", "nonexistent", "Patient1", true);

            var exception = await Assert.ThrowsAsync<DeadLetterException>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Contains("No ReportSchedule found", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_TimeoutException_ThrowsTimeoutException()
        {
            using var scope = _fixture.ServiceProvider.CreateScope();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<ReportSchedule, bool>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());

            var listener = CreateListener(scope, mockScopeFactory);

            var consumeResult = CreateConsumeResult("TestFacility", "testid", "Patient1", true);

            await Assert.ThrowsAsync<TimeoutException>(() => listener.ProcessMessageAsync(consumeResult, default));
        }

        [Fact]
        public async Task ProcessMessageAsync_GeneralException_ThrowsException()
        {
            _fixture.ResetMocks();
            //await _fixture.ClearDatabaseAsync();

            using var scope = _fixture.ServiceProvider.CreateScope();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            mockServiceProvider.Setup(sp => sp.GetService(typeof(IReportScheduledManager))).Returns(new Mock<IReportScheduledManager>().Object);
            mockServiceProvider.Setup(sp => sp.GetService(It.Is<Type>(t => t != typeof(IReportScheduledManager)))).Returns<Type>(t => scope.ServiceProvider.GetService(t));
            mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
            var reportScheduledManagerMock = mockServiceProvider.Object.GetService<IReportScheduledManager>();
            Mock.Get(reportScheduledManagerMock).Setup(m => m.SingleOrDefaultAsync(It.IsAny<Expression<Func<ReportSchedule, bool>>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Test error"));

            var listener = CreateListener(scope, mockScopeFactory);

            var consumeResult = CreateConsumeResult("TestFacility", "testid", "Patient1", true);

            var exception = await Assert.ThrowsAsync<Exception>(() => listener.ProcessMessageAsync(consumeResult, default));
            Assert.Equal("Test error", exception.Message);
        }
    }
}