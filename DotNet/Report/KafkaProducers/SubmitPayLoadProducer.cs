using Confluent.Kafka;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class SubmitPayloadProducer
    {
        private readonly ILogger<SubmitPayloadProducer> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<SubmitPayloadKey, SubmitPayloadValue> _submitPayloadProducer;


        public SubmitPayloadProducer(IServiceScopeFactory serviceScopeFactory, IProducer<SubmitPayloadKey, SubmitPayloadValue> submitPayloadProducer, ILogger<SubmitPayloadProducer> logger)
        {
            _submitPayloadProducer = submitPayloadProducer;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, PayloadType payloadType, string? patientId = null, string? correlationId = null, string? payloadUri = null)
        {
            _logger.LogDebug("Producing SubmitPayload (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", schedule.FacilityId, patientId, schedule.Id);

            var corrId = string.IsNullOrWhiteSpace(correlationId)
                      ? Guid.NewGuid().ToString()
                      : correlationId;

            if (schedule.SubmitReportDateTime.HasValue)
            {
                return false;
            }

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
                        StartDate = schedule.ReportStartDate.UtcDateTime,
                        EndDate = schedule.ReportEndDate.UtcDateTime
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
