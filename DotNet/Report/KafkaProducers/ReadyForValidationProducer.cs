using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class ReadyForValidationProducer
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<ReadyForValidationKey, ReadyForValidationValue> _readyForValidationProducer;
        private readonly IReportEntryManager _reportEntryStatusManager;

        public ReadyForValidationProducer(IProducer<ReadyForValidationKey, ReadyForValidationValue> readyForValidationProducer, IServiceScopeFactory serviceScopeFactory, IReportEntryManager reportEntryStatusManager)
        {
            _readyForValidationProducer = readyForValidationProducer;
            _serviceScopeFactory = serviceScopeFactory;
            _reportEntryStatusManager = reportEntryStatusManager;
        }

        public class ProduceValidationModel
        {
            public required string ReportScheduleId { get; set; }
            public required List<string> ReportTypes { get; set; }
            public required string FacilityId { get; set; }
            public required string PatientId { get; set; }
            public required string? PayloadUri { get; set; }
        }

        public async Task Produce(List<ProduceValidationModel> needValidation)
        {
            foreach (var entry in needValidation)
            {
                await Produce(entry.ReportScheduleId, entry.ReportTypes, entry.FacilityId, entry.PatientId, entry.PayloadUri, Guid.NewGuid().ToString());
            }
        }

        public async Task Produce(string scheduleId, List<string> reportTypes, string facilityId, string patientId, string? payloadUri, string correlationId)
        {
            var corrId = string.IsNullOrWhiteSpace(correlationId)
                       ? Guid.NewGuid().ToString()
                       : correlationId;

            _readyForValidationProducer.Produce(nameof(KafkaTopic.ReadyForValidation),
                new Message<ReadyForValidationKey, ReadyForValidationValue>
                {
                    Key = new ReadyForValidationKey()
                    {
                        FacilityId = facilityId

                    },
                    Value = new ReadyForValidationValue
                    {
                        PatientId = patientId,
                        ReportTypes = reportTypes,
                        ReportTrackingId = scheduleId,
                        PayloadUri = payloadUri
                    },
                    Headers = new Headers
                    {
                        { "X-Correlation-Id",  Encoding.UTF8.GetBytes(corrId) }
                    }
                });

            _readyForValidationProducer.Flush();

            var entry = await _reportEntryStatusManager.GetEntry(scheduleId, patientId);
            entry.ReportingStatus = Domain.Enums.ReportingStatus.PendingValidation;
            entry.SubmissionStatus = Domain.Enums.SubmissionStatus.PendingValidation;
            await _reportEntryStatusManager.UpdateAsync(entry, CancellationToken.None);
        }
    }
}
