using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Core;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class ReadyForValidationProducer
    {
        private readonly IDatabase _database;
        private readonly IProducer<ReadyForValidationKey, ReadyForValidationValue> _readyForValidationProducer;

        public ReadyForValidationProducer(IDatabase database, MeasureReportAggregator aggregator, IProducer<ReadyForValidationKey, ReadyForValidationValue> readyForValidationProducer)
        {
            _readyForValidationProducer = readyForValidationProducer;
            _database = database;
        }


        public async Task<bool> Produce(ReportScheduleModel schedule, IEnumerable<MeasureReportSubmissionEntryModel> needValidation)
        {
            foreach (var entry in needValidation)
            {
                await Produce(schedule, entry);
            }

            return true;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule, MeasureReportSubmissionEntryModel entry)
        {
            _readyForValidationProducer.Produce(nameof(KafkaTopic.ReadyForValidation),
                new Message<ReadyForValidationKey, ReadyForValidationValue>
                {
                    Key = new ReadyForValidationKey()
                    {
                        FacilityId = schedule.FacilityId

                    },
                    Value = new ReadyForValidationValue
                    {
                        PatientId = entry.PatientId,
                        ReportTypes = schedule.ReportTypes,
                        ReportTrackingId = schedule.Id!
                    },
                    Headers = new Headers
                    {
                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                    }
                });

            _readyForValidationProducer.Flush();

            entry.ValidationStatus = ValidationStatus.Requested;
            entry.Status = PatientSubmissionStatus.ValidationRequested;
            await _database.SubmissionEntryRepository.UpdateAsync(entry);

            return true;
        }
    }
}
