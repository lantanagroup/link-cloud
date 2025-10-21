using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Models;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using SubmissionStatus = LantanaGroup.Link.Report.Domain.Enums.SubmissionStatus;
using System.Text;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class ReadyForValidationProducer
    {
        private readonly ILogger<ReadyForValidationProducer> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<ReadyForValidationKey, ReadyForValidationValue> _readyForValidationProducer;

        public ReadyForValidationProducer(IProducer<ReadyForValidationKey, ReadyForValidationValue> readyForValidationProducer, IServiceScopeFactory serviceScopeFactory, ILogger<ReadyForValidationProducer> logger)
        {
            _readyForValidationProducer = readyForValidationProducer;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public class ProduceValidationModel
        {
            public required Guid ReportScheduleId { get; set; }
            public required List<string> ReportTypes { get; set; }
            public required string FacilityId { get; set; }
            public required string PatientId { get; set; }
            public required string? PayloadUri { get; set; }
        }

        public async Task Produce(List<ProduceValidationModel> needValidation, CancellationToken cancellationToken = default)
        {
            foreach (var entry in needValidation)
            {
                await Produce(entry.ReportScheduleId, entry.ReportTypes, entry.FacilityId, entry.PatientId, entry.PayloadUri, Guid.NewGuid().ToString(), cancellationToken);
            }
        }

        public async Task Produce(Guid scheduleId, List<string> reportTypes, string facilityId, string patientId, string? payloadUri, string correlationId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The PendingValidation status must be persisted before the message is produced.
            // Otherwise a fast ValidationComplete round-trip can set PassedValidation/FailedValidation
            // first, and the write below would regress the entry to PendingValidation after a
            // SubmitPayload was already produced, permanently blocking report completion.
            using var scope = _serviceScopeFactory.CreateScope();
            var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();
            var entry = await reportEntryManager.GetEntry(scheduleId, patientId, cancellationToken);

            if (entry == null)
            {
                throw new Exception($"No report entry record was found (ReportId = {scheduleId}, FacilityId = {facilityId}).");
            }

            entry.ReportingStatus = ReportingStatus.PendingValidation;
            entry.SubmissionStatus = SubmissionStatus.PendingValidation;

            await reportEntryManager.UpdateAsync(entry, cancellationToken);

            _logger.LogDebug("Producing ReadyForValidation (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", facilityId.SanitizeForLog(), patientId.SanitizeForLog(), scheduleId.SanitizeForLog());

            _readyForValidationProducer.Produce(nameof(KafkaTopic.ReadyForValidation),
                new Message<ReadyForValidationKey, ReadyForValidationValue>
                {
                    Key = new ReadyForValidationKey()
                    {
                        FacilityId = facilityId,
                        CorrelationId = correlationId
                    },
                    Value = new ReadyForValidationValue
                    {
                        PatientId = patientId,
                        ReportTypes = reportTypes,
                        ReportTrackingId = scheduleId.ToString(),
                        PayloadUri = payloadUri
                    },
                    Headers = new Headers
                    {
                        { "X-Correlation-Id",  Encoding.UTF8.GetBytes(correlationId) }
                    }
                });

            _readyForValidationProducer.Flush();
        }
    }
}
