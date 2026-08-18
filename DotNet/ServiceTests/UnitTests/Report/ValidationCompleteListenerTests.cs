using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Report.Application.Core;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.KafkaProducers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers the PreQualification:WritePreQualOperationOutcome gate (LEGLINK-466). With the flag
/// set, the Validation service is the sole writer of the pre-qualification OperationOutcome
/// (LEGLINK-425) and Report must not append its own.
/// </summary>
[Trait("Category", "UnitTests")]
public class ValidationCompleteListenerTests
{
    private const string FacilityId = "facility-a";
    private const string PatientId = "patient-1";
    private const string BlobName = "report/patient-1.ndjson";

    [Fact]
    public async Task ProcessMessageAsync_WhenInvalidAndFlagOff_AppendsOperationOutcome()
    {
        var harness = new Harness(writePreQualOperationOutcome: false);

        await harness.Listener.ProcessMessageAsync(CreateConsumeResult(harness.ReportId, isValid: false), CancellationToken.None);

        harness.PatientAggregator.Verify(
            a => a.AppendResourceToBlob(BlobName, It.IsAny<OperationOutcome>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenInvalidAndFlagOn_DoesNotAppendOperationOutcome()
    {
        var harness = new Harness(writePreQualOperationOutcome: true);

        await harness.Listener.ProcessMessageAsync(CreateConsumeResult(harness.ReportId, isValid: false), CancellationToken.None);

        harness.PatientAggregator.Verify(
            a => a.AppendResourceToBlob(It.IsAny<string>(), It.IsAny<DomainResource>()),
            Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProcessMessageAsync_WhenValid_NeverAppendsRegardlessOfFlag(bool writePreQualOperationOutcome)
    {
        var harness = new Harness(writePreQualOperationOutcome);

        await harness.Listener.ProcessMessageAsync(CreateConsumeResult(harness.ReportId, isValid: true), CancellationToken.None);

        harness.PatientAggregator.Verify(
            a => a.AppendResourceToBlob(It.IsAny<string>(), It.IsAny<DomainResource>()),
            Times.Never);
    }

    /// <summary>
    /// The flag gates only the blob append — the entry status update and the SubmitPayload
    /// produce must still happen when it is on.
    /// </summary>
    [Fact]
    public async Task ProcessMessageAsync_WhenFlagOn_StillUpdatesEntryAndProducesSubmitPayload()
    {
        var harness = new Harness(writePreQualOperationOutcome: true);

        await harness.Listener.ProcessMessageAsync(CreateConsumeResult(harness.ReportId, isValid: false), CancellationToken.None);

        harness.ReportEntryManager.Verify(
            m => m.UpdateAsync(
                It.Is<ReportEntryModel>(e => e.ReportingStatus == ReportingStatus.FailedValidation),
                It.IsAny<CancellationToken>()),
            Times.Once);

        harness.Producer.Verify(
            p => p.Produce(
                nameof(KafkaTopic.SubmitPayload),
                It.IsAny<Message<SubmitPayloadKey, SubmitPayloadValue>>(),
                It.IsAny<Action<DeliveryReport<SubmitPayloadKey, SubmitPayloadValue>>>()),
            Times.Once);
    }

    private static ConsumeResult<string, ValidationCompleteValue> CreateConsumeResult(Guid reportId, bool isValid)
    {
        return new ConsumeResult<string, ValidationCompleteValue>
        {
            Message = new Message<string, ValidationCompleteValue>
            {
                Key = FacilityId,
                Value = new ValidationCompleteValue
                {
                    PatientId = PatientId,
                    IsValid = isValid,
                    ReportTrackingId = reportId.ToString()
                },
                Headers = new Headers
                {
                    { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                }
            }
        };
    }

    private sealed class Harness
    {
        public Guid ReportId { get; } = Guid.NewGuid();
        public Mock<PatientAggregator> PatientAggregator { get; }
        public Mock<IReportEntryManager> ReportEntryManager { get; } = new();
        public Mock<IProducer<SubmitPayloadKey, SubmitPayloadValue>> Producer { get; } = new();
        public ValidationCompleteListener Listener { get; }

        public Harness(bool writePreQualOperationOutcome)
        {
            var blobSettings = Options.Create(new BlobStorageSettings
            {
                ConnectionString = "UseDevelopmentStorage=true",
                BlobContainerName = "internal"
            });

            var schedule = new ReportScheduleModel
            {
                Id = ReportId,
                FacilityId = FacilityId,
                ReportStartDate = DateTimeOffset.UtcNow.AddDays(-30),
                ReportEndDate = DateTimeOffset.UtcNow
            };

            var entry = new ReportEntryModel
            {
                ReportScheduleId = ReportId,
                PatientId = PatientId,
                AggregateReportBlobName = BlobName,
                AggregateReportUri = $"https://blob/{BlobName}"
            };

            var scheduledManager = new Mock<IReportScheduledManager>();
            scheduledManager
                .Setup(m => m.SingleOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<LantanaGroup.Link.Report.Data.Entities.ReportSchedule, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(schedule);

            ReportEntryManager
                .Setup(m => m.GetEntry(ReportId, PatientId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);
            ReportEntryManager
                .Setup(m => m.UpdateAsync(It.IsAny<ReportEntryModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ReportEntryModel m, CancellationToken _) => m);

            PatientAggregator = new Mock<PatientAggregator>(
                Mock.Of<IReportServiceMetrics>(),
                ReportEntryManager.Object,
                new BlobStorageService(blobSettings),
                blobSettings,
                Mock.Of<ITenantApiService>(),
                Options.Create(new PatientAggregatorSettings()));
            PatientAggregator
                .Setup(a => a.AppendResourceToBlob(It.IsAny<string>(), It.IsAny<DomainResource>()))
                .Returns(Task.CompletedTask);

            var services = new ServiceCollection();
            services.AddScoped(_ => scheduledManager.Object);
            services.AddScoped(_ => ReportEntryManager.Object);
            services.AddScoped(_ => PatientAggregator.Object);

            var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

            var submitPayloadProducer = new SubmitPayloadProducer(
                scopeFactory,
                Producer.Object,
                Mock.Of<ILogger<SubmitPayloadProducer>>());

            Listener = new ValidationCompleteListener(
                Mock.Of<ILogger<ValidationCompleteListener>>(),
                Mock.Of<IKafkaConsumerFactory<string, ValidationCompleteValue>>(),
                Mock.Of<ITransientExceptionHandler<ValidationCompleteListener, string, ValidationCompleteValue>>(),
                Mock.Of<IDeadLetterExceptionHandler<ValidationCompleteListener, string, ValidationCompleteValue>>(),
                submitPayloadProducer,
                scopeFactory,
                new ServiceInformation { ServiceConfigName = "Report" },
                new BlobStorageService(blobSettings),
                Options.Create(new PreQualificationSettings { WritePreQualOperationOutcome = writePreQualOperationOutcome }),
                Mock.Of<IExceptionLogger<ValidationCompleteListener>>());
        }
    }
}
