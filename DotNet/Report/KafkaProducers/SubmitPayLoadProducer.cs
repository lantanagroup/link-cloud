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

            if (schedule.SubmitReportDateTime.HasValue)
            {
                return false;
            }

            var submissionEntries = await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && (patientId == null || (x.PatientId == patientId && x.Status != PatientSubmissionStatus.NotReportable)));

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
                        ReportTypes = schedule.ReportTypes,
                        StartDate = schedule.ReportStartDate,
                        EndDate = schedule.ReportEndDate
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
