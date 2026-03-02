using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report
{
    [Collection("ReportIntegrationTests")]
    public class ValidationCompleteListenerTests : IClassFixture<ReportIntegrationTestFixture>
    {
        private readonly ReportIntegrationTestFixture _fixture;

        public ValidationCompleteListenerTests(ReportIntegrationTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ProcessMessageAsync_NullResult_ThrowsNullReferenceException()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ValidationCompleteListener>();

            var consumeResult = (ConsumeResult<string, ValidationCompleteValue>)null!;

            await Assert.ThrowsAsync<NullReferenceException>(
                () => listener.ProcessMessageAsync(consumeResult, CancellationToken.None));
        }

        [Fact]
        public async Task ProcessMessageAsync_MissingCorrelationId_ThrowsDeadLetterException()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ValidationCompleteListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

            var facilityId = "test-facility-validation";
            var reportId = Guid.NewGuid().ToString();
            var patientId = "pat-001";

            var schedule = new ReportSchedule
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var value = new ValidationCompleteValue
            {
                PatientId = patientId,
                IsValid = true,
                ReportTrackingId = reportId
            };

            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = facilityId,
                    Value = value,
                    Headers = new Headers()
                }
            };

            var exception = await Assert.ThrowsAsync<DeadLetterException>(
                () => listener.ProcessMessageAsync(consumeResult, CancellationToken.None));

            Assert.Contains("without correlation ID", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_NoScheduleFound_ThrowsDeadLetterException()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ValidationCompleteListener>();

            var facilityId = "test-facility-validation";
            var reportId = Guid.NewGuid().ToString();
            var patientId = "pat-001";

            var value = new ValidationCompleteValue
            {
                PatientId = patientId,
                IsValid = true,
                ReportTrackingId = reportId
            };

            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = facilityId,
                    Value = value,
                    Headers = headers
                }
            };

            var exception = await Assert.ThrowsAsync<DeadLetterException>(
                () => listener.ProcessMessageAsync(consumeResult, CancellationToken.None));

            Assert.Contains("No scheduled report record was found", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_NoReportEntryFound_ThrowsDeadLetterException()
        {
            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ValidationCompleteListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();

            var facilityId = "test-facility-validation";
            var reportId = Guid.NewGuid().ToString();
            var patientId = "pat-001";

            var schedule = new ReportSchedule
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var value = new ValidationCompleteValue
            {
                PatientId = patientId,
                IsValid = true,
                ReportTrackingId = reportId
            };

            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = facilityId,
                    Value = value,
                    Headers = headers
                }
            };

            var exception = await Assert.ThrowsAsync<DeadLetterException>(
                () => listener.ProcessMessageAsync(consumeResult, CancellationToken.None));

            Assert.Contains("No patient report entry records were found", exception.Message);
        }

        [Fact]
        public async Task ProcessMessageAsync_ValidValidation_UpdatesEntryAndProducesSubmitPayload()
        {
            _fixture.SubmitPayloadKafkaProducerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ValidationCompleteListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

            var facilityId = "test-facility-validation";
            var reportId = Guid.NewGuid().ToString();
            var patientId = "pat-001";

            var schedule = new ReportSchedule
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var entry = new ReportEntry
            {
                PatientId = patientId,
                ReportScheduleId = reportId,
                FacilityId = facilityId,
                ReportingStatus = ReportingStatus.PatientIdentified,
                SubmissionStatus = SubmissionStatus.Submitting,
                CreateDate = DateTime.UtcNow,
                AggregateReportBlobName = "test-aggregate.ndjson",
                AggregateReportUri = "https://blob.example.com/test-aggregate.ndjson"
            };
            await reportEntryManager.AddAsync(entry, CancellationToken.None);

            var value = new ValidationCompleteValue
            {
                PatientId = patientId,
                IsValid = true,
                ReportTrackingId = reportId
            };

            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = facilityId,
                    Value = value,
                    Headers = headers
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyEntryManager = verifyScope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var updatedEntry = await verifyEntryManager.SingleOrDefaultAsync(e => e.PatientId == patientId && e.ReportScheduleId == reportId);

            Assert.Equal(ReportingStatus.PassedValidation, updatedEntry.ReportingStatus);
            Assert.Equal(SubmissionStatus.Submitting, updatedEntry.SubmissionStatus);

            _fixture.SubmitPayloadKafkaProducerMock.Verify(
                p => p.Produce(
                    It.IsAny<string>(),
                    It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                        m.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry &&
                        m.Value.PatientId == patientId),
                    It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_InvalidValidation_UpdatesEntryAndProducesSubmitPayload()
        {
            _fixture.SubmitPayloadKafkaProducerMock.Reset();

            using var scope = _fixture.ScopeFactory.CreateScope();
            var listener = scope.ServiceProvider.GetRequiredService<ValidationCompleteListener>();
            var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

            var facilityId = "test-facility-validation";
            var reportId = Guid.NewGuid().ToString();
            var patientId = "pat-001";

            var schedule = new ReportSchedule
            {
                Id = reportId,
                FacilityId = facilityId,
                ReportStartDate = DateTime.UtcNow.AddDays(-30),
                ReportEndDate = DateTime.UtcNow.AddDays(30),
                Frequency = Frequency.Monthly,
                ReportTypes = new List<string> { "DE-111" },
                Status = ScheduleStatus.Scheduled,
                CreateDate = DateTime.UtcNow
            };
            await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

            var entry = new ReportEntry
            {
                PatientId = patientId,
                ReportScheduleId = reportId,
                FacilityId = facilityId,
                ReportingStatus = ReportingStatus.PatientIdentified,
                SubmissionStatus = SubmissionStatus.Submitting,
                CreateDate = DateTime.UtcNow,
                AggregateReportBlobName = "test-aggregate.ndjson",
                AggregateReportUri = "https://blob.example.com/test-aggregate.ndjson"
            };
            await reportEntryManager.AddAsync(entry, CancellationToken.None);

            await CreateAppendBlobForTest(entry.AggregateReportBlobName);

            var value = new ValidationCompleteValue
            {
                PatientId = patientId,
                IsValid = false,
                ReportTrackingId = reportId
            };

            var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

            var consumeResult = new ConsumeResult<string, ValidationCompleteValue>
            {
                Message = new Message<string, ValidationCompleteValue>
                {
                    Key = facilityId,
                    Value = value,
                    Headers = headers
                }
            };

            await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);

            using var verifyScope = _fixture.ScopeFactory.CreateScope();
            var verifyEntryManager = verifyScope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var updatedEntry = await verifyEntryManager.SingleOrDefaultAsync(e => e.PatientId == patientId && e.ReportScheduleId == reportId);

            Assert.Equal(ReportingStatus.FailedValidation, updatedEntry.ReportingStatus);
            Assert.Equal(SubmissionStatus.Submitting, updatedEntry.SubmissionStatus);

            _fixture.SubmitPayloadKafkaProducerMock.Verify(
                p => p.Produce(
                    It.IsAny<string>(),
                    It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m =>
                        m.Value.PayloadType == PayloadType.MeasureReportSubmissionEntry &&
                        m.Value.PatientId == patientId),
                    It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
                Times.Once);
        }

        private async Task CreateAppendBlobForTest(string blobName)
        {
            var containerClient = new BlobContainerClient(_fixture.AzuriteConnectionString, "report-test-container");
            var appendBlobClient = containerClient.GetAppendBlobClient(blobName);

            await appendBlobClient.CreateIfNotExistsAsync();

            string initialContent = "{\"resourceType\":\"Bundle\",\"id\":\"initial\"}\n";
            var bytes = Encoding.UTF8.GetBytes(initialContent);
            using var stream = new MemoryStream(bytes);
            await appendBlobClient.AppendBlockAsync(stream);
        }
    }
}