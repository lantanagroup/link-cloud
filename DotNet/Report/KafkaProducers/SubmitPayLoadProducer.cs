using Confluent.Kafka;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class SubmitPayloadProducer
    {
        private readonly ILogger<SubmitPayloadProducer> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IProducer<string, SubmitPayloadValue> _submitPayloadProducer;


        public SubmitPayloadProducer(IServiceScopeFactory serviceScopeFactory, IProducer<string, SubmitPayloadValue> submitPayloadProducer, ILogger<SubmitPayloadProducer> logger)
        {
            _submitPayloadProducer = submitPayloadProducer;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, PayloadType payloadType, string? patientId = null, string? correlationId = null, string? payloadUri = null, string? metricsMode = null, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Producing SubmitPayload (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", schedule.FacilityId.SanitizeForLog(), patientId.SanitizeForLog(), schedule.Id.SanitizeForLog());

            var corrId = string.IsNullOrWhiteSpace(correlationId)
                      ? Guid.NewGuid().ToString()
                      : correlationId;

            if (schedule.SubmitReportDateTime.HasValue)
            {
                return false;
            }

            var key = string.IsNullOrWhiteSpace(patientId)
                ? KafkaKeys.ForFacility(schedule.FacilityId)
                : KafkaKeys.ForPatient(schedule.FacilityId, patientId);

            await _submitPayloadProducer.ProduceAsync(nameof(KafkaTopic.SubmitPayload),
                new Message<string, SubmitPayloadValue>
                {
                    Key = key,
                    Value = new SubmitPayloadValue()
                    {
                        PayloadType = payloadType,
                        PatientId = patientId,
                        PayloadUri = payloadUri,
                        ReportTypes = schedule.ReportTypes,
                        StartDate = schedule.ReportStartDate.UtcDateTime,
                        EndDate = schedule.ReportEndDate.UtcDateTime,
                        FacilityId = schedule.FacilityId,
                        ReportScheduleId = schedule.Id
                    },

                    Headers = CreateHeaders(corrId, metricsMode)
                },
                cancellationToken);

            return true;
        }

        private static Headers CreateHeaders(string correlationId, string? metricsMode)
        {
            var headers = new Headers
            {
                { "X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId) }
            };
            if (!string.IsNullOrWhiteSpace(metricsMode))
            {
                KafkaHeaderHelper.SetMetricsMode(headers, metricsMode);
            }

            return headers;
        }
    }
}
