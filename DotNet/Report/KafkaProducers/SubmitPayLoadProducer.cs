using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class SubmitPayloadProducer
    {
        private readonly IDatabase _database;
        private readonly IProducer<SubmitPayloadKey, SubmitPayloadValue> _submitPayloadProducer;


        public SubmitPayloadProducer(IDatabase database, IProducer<SubmitPayloadKey, SubmitPayloadValue> submitPayloadProducer) 
        {
            _submitPayloadProducer = submitPayloadProducer;
            _database = database;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, PayloadType payloadType, string? patientId = null, string? correlationId = null, string? payloadUri = null)
        {

            var corrId = string.IsNullOrWhiteSpace(correlationId)
                      ? Guid.NewGuid().ToString()
                      : correlationId;

            if (string.IsNullOrEmpty(payloadUri))
            {
                throw new InvalidOperationException("payloadUri is null or empty - cannot produce SubmitPayload event");
            }

            if (schedule.SubmitReportDateTime.HasValue)
            {
                return false;
            }

            var submissionEntries = await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && (patientId == null || (x.PatientId == patientId && x.Status != PatientSubmissionStatus.NotReportable)));

            var measureIds = submissionEntries
                        .Where(e => e.MeasureReport?.Measure != null)
                        .Select(e => e.MeasureReport!.Measure)
                        .Distinct()
                        .ToList();

            _submitPayloadProducer.Produce(nameof(KafkaTopic.SubmitPayload),
                new Message<SubmitPayloadKey, SubmitPayloadValue>
                {
                    Key = new SubmitPayloadKey()
                    {
                        FacilityId = schedule.FacilityId,
                        ReportScheduleId = schedule.Id
                    },
                    Value = new SubmitPayloadValue()
                    {
                        PayloadType = payloadType,
                        PatientId = patientId,
                        PayloadUri = payloadUri,
                        MeasureIds = measureIds
                    },

                    Headers = new Headers
                    {
                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(corrId) }
                    }
                });

            _submitPayloadProducer.Flush();         

            return true;
        }

    }
}
