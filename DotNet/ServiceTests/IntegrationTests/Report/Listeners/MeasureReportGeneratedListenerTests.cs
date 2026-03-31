using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Listeners;

[Collection("ReportIntegrationTests")]
public class MeasureReportGeneratedListenerTests : IClassFixture<ReportIntegrationTestFixture>
{
    private readonly ReportIntegrationTestFixture _fixture;

    public MeasureReportGeneratedListenerTests(ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ReportScheduleModel CreateSchedule(string facilityId, Guid reportId)
    {
        return new ReportScheduleModel
        {
            Id = reportId,
            FacilityId = facilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(30),
            Frequency = Frequency.Monthly,
            ReportTypes = { "DE-111" },
            Status = ScheduleStatus.Scheduled,
            CreateDate = DateTime.UtcNow,
            EndOfReportPeriodJobHasRun = true,
            EnableSubmission = true,
            PayloadRootUri = "https://blob.example.com/reports/" + reportId
        };
    }

    private ReportEntryModel CreateEntry(string facilityId, Guid reportId, string patientId, MeasureReportStatus status, string measureReportFileName, ReportingStatus reportingStatus = ReportingStatus.PatientIdentified, SubmissionStatus? submissionStatus = null)
    {
        return new ReportEntryModel
        {
            FacilityId = facilityId,
            ReportScheduleId = reportId,
            PatientId = patientId,
            ReportingStatus = reportingStatus,
            SubmissionStatus = submissionStatus,
            CreateDate = DateTime.UtcNow,
            MeasureReports = new List<EntryMeasureReportModel>
            {
                new EntryMeasureReportModel
                {
                    ReportType = "DE-111",
                    Status = status,
                    MeasureReportId = Guid.NewGuid().ToString(),
                    MeasureReportUri = "https://blob.example.com/measure/" + Guid.NewGuid(),
                    MeasureReportFileName = measureReportFileName,
                    ResourceCount = new Dictionary<string, int>()
                    {
                        { "Patient", 1 },
                        { "Observation", 5 }
                    }
                }
            }
        };
    }

    private async Task UploadDummyMeasureReportBlob(string blobName)
    {
        var containerClient = new BlobContainerClient(_fixture.AzuriteConnectionString, "report-test-container");
        var blobClient = containerClient.GetBlockBlobClient(blobName);

        string referenceLine = $"MeasureReport/{Guid.NewGuid()}";
        string measureReportJson = @"{""resourceType"":""MeasureReport"",""id"":""test-mr"",""status"":""complete"",""type"":""individual"",""measure"":""http://example.com/Measure/DE-111"",""subject"":{""reference"":""Patient/pat-123""},""period"":{""start"":""2024-01-01"",""end"":""2024-01-31""},""group"":[{""population"":[{""code"":{""coding"":[{""system"":""http://terminology.hl7.org/CodeSystem/measure-population"",""code"":""initial-population""}],""text"":""Initial Population""},""count"":1}]}]}";
        string dummyContent = referenceLine + "\n" + measureReportJson + "\n";

        var contentBytes = Encoding.UTF8.GetBytes(dummyContent);
        using var stream = new MemoryStream(contentBytes);
        await blobClient.UploadAsync(stream);
    }

    [Fact]
    public async Task ProcessMessageAsync_NullValue_CallsDeadLetterHandler()
    {
        _fixture.MeasureReportGeneratedDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();

        var facilityId = "test-facility-011";

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = null! }
        };

        await Assert.ThrowsAsync<DeadLetterException>(async () =>
            await listener.ProcessMessageAsync(consumeResult, facilityId, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessMessageAsync_NoCorrelationId_CallsDeadLetterHandler()
    {
        _fixture.MeasureReportGeneratedDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();

        var facilityId = "test-facility-012";
        var reportId = Guid.NewGuid().ToString();

        var value = new MeasureReportGeneratedValue { FacilityId = facilityId, ReportTrackingId = reportId, PatientId = "pat-123" };

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = value, Headers = new Headers() }
        };

        await Assert.ThrowsAsync<DeadLetterException>(async () =>
            await listener.ProcessMessageAsync(consumeResult, facilityId, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessMessageAsync_NoSchedule_CallsDeadLetterHandler()
    {
        _fixture.MeasureReportGeneratedDeadLetterHandlerMock.Reset();

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();

        var facilityId = "test-facility-013";
        var reportId = Guid.NewGuid().ToString();

        var value = new MeasureReportGeneratedValue { FacilityId = facilityId, ReportTrackingId = reportId, PatientId = "pat-123" };

        var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = value, Headers = headers }
        };

        await Assert.ThrowsAsync<DeadLetterException>(async () =>
            await listener.ProcessMessageAsync(consumeResult, facilityId, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessMessageAsync_AllNonReportable_UpdatesAndProducesManifest()
    {
        _fixture.SubmitPayloadKafkaProducerMock.Reset();
        _fixture.FacilityServiceClientMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityModel { FacilityName = "Test Facility" });

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "test-facility-014";
        var reportId = Guid.NewGuid();
        var measureReportBlobName = "measure-" + Guid.NewGuid() + ".json";

        var schedule = CreateSchedule(facilityId, reportId);
        await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

        var entry = CreateEntry(facilityId, reportId, "pat-123", MeasureReportStatus.NotReportable, measureReportBlobName, ReportingStatus.NotReportable, SubmissionStatus.NotEligable);
        await reportEntryManager.AddAsync(entry, CancellationToken.None);

        await UploadDummyMeasureReportBlob(measureReportBlobName);

        var value = new MeasureReportGeneratedValue
        {
            FacilityId = facilityId,
            ReportTrackingId = reportId.ToString(),
            PatientId = "pat-123",
            ReportType = "DE-111",
            MeasureReportId = Guid.NewGuid().ToString(),
            MeasureReportBlobName = measureReportBlobName,
            MeasureReportURI = "https://blob.example.com/measure/" + Guid.NewGuid(),
            Reportable = false
        };

        var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = value, Headers = headers }
        };

        await listener.ProcessMessageAsync(consumeResult, facilityId, CancellationToken.None);

        _fixture.SubmitPayloadKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m => m.Value.PayloadType == PayloadType.ReportSchedule),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_NotReadyForAggregation_ProducesManifest()
    {
        _fixture.SubmitPayloadKafkaProducerMock.Reset();
        _fixture.FacilityServiceClientMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacilityModel { FacilityName = "Test Facility" });

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "test-facility-015";
        var reportId = Guid.NewGuid();
        var measureReportBlobName = "measure-" + Guid.NewGuid() + ".json";

        var schedule = CreateSchedule(facilityId, reportId);
        await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

        // Setup to explicitly enter the !readyForAggregation block AND pass the foreach in ReportManifestProducer.Produce():
        //   - Two measures: DE-111 gets updated to NotReportable, DE-222 stays EntryCreated → readyForAggregation = false
        //   - Parent ReportEntry statuses = NotReportable + NotEligable → foreach never returns false
        var entry = new ReportEntryModel
        {
            FacilityId = facilityId,
            ReportScheduleId = reportId,
            PatientId = "pat-123",
            ReportingStatus = ReportingStatus.NotReportable,
            SubmissionStatus = SubmissionStatus.NotEligable,
            CreateDate = DateTime.UtcNow,
            MeasureReports = new List<EntryMeasureReportModel>
            {
                new EntryMeasureReportModel
                {
                    ReportType = "DE-111",
                    Status = MeasureReportStatus.EntryCreated,
                    MeasureReportId = Guid.NewGuid().ToString(),
                    MeasureReportUri = "https://blob.example.com/measure/" + Guid.NewGuid(),
                    MeasureReportFileName = measureReportBlobName,
                    ResourceCount = new Dictionary<string, int>()
                     {
                        { "Patient", 1 },
                        { "Observation", 5 }
                     }
                },
                new EntryMeasureReportModel
                {
                    ReportType = "DE-222",
                    Status = MeasureReportStatus.EntryCreated,
                    MeasureReportId = Guid.NewGuid().ToString(),
                    MeasureReportUri = "https://blob.example.com/measure/" + Guid.NewGuid(),
                    MeasureReportFileName = "measure-" + Guid.NewGuid() + ".json",
                    ResourceCount = new Dictionary<string, int>()
                     {
                        { "Patient", 1 },
                        { "Observation", 5 }
                     }
                }
            }
        };
        await reportEntryManager.AddAsync(entry, CancellationToken.None);

        await UploadDummyMeasureReportBlob(measureReportBlobName);

        var value = new MeasureReportGeneratedValue
        {
            FacilityId = facilityId,
            ReportTrackingId = reportId.ToString(),
            PatientId = "pat-123",
            ReportType = "DE-111",
            MeasureReportId = Guid.NewGuid().ToString(),
            MeasureReportBlobName = measureReportBlobName,
            MeasureReportURI = "https://blob.example.com/measure/" + Guid.NewGuid(),
            Reportable = false
        };

        var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = value, Headers = headers }
        };

        await listener.ProcessMessageAsync(consumeResult, facilityId, CancellationToken.None);

        _fixture.SubmitPayloadKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<SubmitPayloadKey, SubmitPayloadValue>>(m => m.Value.PayloadType == PayloadType.ReportSchedule),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ReadyForAggregation_PerformsFullAggregationAndProducesReadyForValidation()
    {
        var facilityId = "test-facility-016";

        _fixture.ReadyForValidationKafkaProducerMock.Reset();
        _fixture.FacilityServiceClientMock.Setup(x => x.GetAsync(facilityId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new FacilityModel { FacilityId = facilityId, FacilityName = "Test Facility" });

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var reportId = Guid.NewGuid();
        var measureReportBlobName = "measure-" + Guid.NewGuid() + ".json";

        var schedule = CreateSchedule(facilityId, reportId);
        await reportScheduledManager.AddAsync(schedule, CancellationToken.None);

        var entry = CreateEntry(facilityId, reportId, "pat-123", MeasureReportStatus.ReadyForValidation, measureReportBlobName, ReportingStatus.PatientIdentified, null);
        await reportEntryManager.AddAsync(entry, CancellationToken.None);

        await UploadDummyMeasureReportBlob(measureReportBlobName);

        var value = new MeasureReportGeneratedValue
        {
            FacilityId = facilityId,
            ReportTrackingId = reportId.ToString(),
            PatientId = "pat-123",
            ReportType = "DE-111",
            MeasureReportId = Guid.NewGuid().ToString(),
            MeasureReportBlobName = measureReportBlobName,
            MeasureReportURI = "https://blob.example.com/measure/" + Guid.NewGuid(),
            Reportable = true
        };

        var headers = new Headers { { "X-Correlation-Id", Encoding.UTF8.GetBytes("corr-123") } };

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = value, Headers = headers, }
        };

        await listener.ProcessMessageAsync(consumeResult, facilityId, CancellationToken.None);

        _fixture.ReadyForValidationKafkaProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<ReadyForValidationKey, ReadyForValidationValue>>(m => m.Value.PatientId == "pat-123"),
                It.IsAny<Action<DeliveryReport<ReadyForValidationKey, ReadyForValidationValue>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_NullMessageValue_ThrowsDeadletter()
    {
        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<MeasureReportGeneratedListener>();

        var consumeResult = new ConsumeResult<Null, MeasureReportGeneratedValue>
        {
            Message = new Message<Null, MeasureReportGeneratedValue> { Value = null! }
        };

        await Assert.ThrowsAsync<DeadLetterException>(async () =>
                await listener.ProcessMessageAsync(consumeResult, "willthrow", CancellationToken.None));
    }
}