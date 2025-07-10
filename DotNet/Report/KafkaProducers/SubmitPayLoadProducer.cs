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
        private readonly IProducer<SubmitPayloadKey, SubmitPayloadValue> _submitPayLoadProducer;


        public SubmitPayloadProducer(IDatabase database, IProducer<SubmitPayloadKey, SubmitPayloadValue> submitPayLoadProducer) 
        {
            _submitPayLoadProducer = submitPayLoadProducer;
            _database = database;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, PayloadType payloadType,  string patientId, string? payloadUri)
        {
            if(string.IsNullOrEmpty(payloadUri))
            {
                throw new InvalidOperationException("payloadUri is null or empty - cannot produce SubmitPayload event");
            }

            if(schedule.SubmitReportDateTime.HasValue)
            {
                return false;
            }

            var submissionEntries = await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && x.PatientId == patientId && x.Status != PatientSubmissionStatus.NotReportable);

            var measureReports = submissionEntries
                        .Where(e => e.MeasureReport?.Measure != null)
                        .Select(e => e.MeasureReport!.Measure)
                        .Distinct()
                        .ToList();

            _submitPayLoadProducer.Produce(nameof(KafkaTopic.SubmitPayload),
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
                        PayLoadUri = payloadUri,
                        MeasureIds = measureReports
                    },

                    Headers = new Headers
                    {
                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                    }
                });

            _submitPayLoadProducer.Flush();

            foreach (var e in submissionEntries)
            {
                e.Status = PatientSubmissionStatus.Submitted;
                await _database.SubmissionEntryRepository.UpdateAsync(e);
            }

            return true;
        }

    }
}
