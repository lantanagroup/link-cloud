using Confluent.Kafka;
using LantanaGroup.Link.Report.Application.Models;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Entities;
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


        public async Task Produce(ReportScheduleModel schedule, IEnumerable<MeasureReportSubmissionEntryModel> needValidation)
        {
            var submissionEntryManager = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ISubmissionEntryManager>();

            foreach (var entry in needValidation)
            {
               await Produce(schedule, entry, submissionEntryManager);
            }
        }

        public async Task Produce(ReportScheduleModel schedule, MeasureReportSubmissionEntryModel entry, ISubmissionEntryManager? manager = null)
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
                        ReportTrackingId = schedule.Id!,
                        PayloadUri = entry.PayloadUri
                    },
                    Headers = new Headers
                    {
                        { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                    }
                });

            _readyForValidationProducer.Flush();

            if(manager == null)
            {
                manager = _serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<ISubmissionEntryManager>();
            }

            await manager.UpdateStatusToValidationRequested(entry.Id);
        }
    }
}
