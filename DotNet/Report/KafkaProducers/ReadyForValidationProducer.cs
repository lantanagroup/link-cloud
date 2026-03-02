using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;

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
            _logger.LogDebug("Producing ReadyForValidation (Facility = {FacilityId}, PatientId = {PatientId}, ReportScheduleId = {ReportScheduleId})", facilityId, patientId, scheduleId);

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
                        { "X-Correlation-Id",  Encoding.UTF8.GetBytes(correlationId) }
                    }
                });

            _readyForValidationProducer.Flush();

            var reportEntryManager = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<IReportEntryManager>();
            var entry = await reportEntryManager.GetEntry(scheduleId, patientId);

            if (entry == null) 
            {
                throw new Exception($"No report entry record was found (ReportId = {scheduleId}, FacilityId = {facilityId}).");
            }

            entry.ReportingStatus = Domain.Enums.ReportingStatus.PendingValidation;
            entry.SubmissionStatus = Domain.Enums.SubmissionStatus.PendingValidation;
            
            await reportEntryManager.UpdateAsync(entry);
        }
    }
}
