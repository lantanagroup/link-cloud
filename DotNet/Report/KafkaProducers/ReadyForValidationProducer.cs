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

        public ReadyForValidationProducer(IProducer<ReadyForValidationKey, ReadyForValidationValue> readyForValidationProducer, IServiceScopeFactory serviceScopeFactory)
        {
            _readyForValidationProducer = readyForValidationProducer;
            _serviceScopeFactory = serviceScopeFactory;
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
            var submissionEntryManager = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ISubmissionEntryManager>();

            foreach (var entry in needValidation)
            {
               await Produce(entry.ReportScheduleId, entry.ReportTypes, entry.FacilityId, entry.PatientId, entry.PayloadUri, Guid.NewGuid().ToString(), submissionEntryManager);
            }
        }

        public async Task Produce(string scheduleId, List<string> reportTypes, string facilityId, string patientId, string? payloadUri, string correlationId, ISubmissionEntryManager? manager = null)
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

            if(manager == null)
            {
                manager = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ISubmissionEntryManager>();
            }

            await manager.UpdateStatusToValidationRequested(scheduleId, facilityId, patientId, CancellationToken.None);
        }
    }
}
