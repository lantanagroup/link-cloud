using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using System.Text;

namespace LantanaGroup.Link.Report.KafkaProducers
{
    public class DataAcquisitionRequestedProducer
    {
        private readonly IDatabase _database;
        private readonly IProducer<string, DataAcquisitionRequestedValue> _dataAcqProducer;

        public DataAcquisitionRequestedProducer(IDatabase database, IProducer<string, DataAcquisitionRequestedValue> dataAcqProducer) 
        {
            _database = database;
            _dataAcqProducer = dataAcqProducer;
        }

        public async Task<bool> Produce(ReportScheduleModel schedule)
        {
            var patientsToEvaluate = (await _database.SubmissionEntryRepository.FindAsync(x => x.ReportScheduleId == schedule.Id && x.Status == PatientSubmissionStatus.PendingEvaluation)).Select(x => x.PatientId).Distinct().ToList();

            foreach (string patientId in patientsToEvaluate)
            {
                var darKey = schedule.FacilityId;

                string reportableEvent = string.Empty;

                switch (schedule.Frequency)
                {
                    case Frequency.Monthly:
                        reportableEvent = "EOM";
                        break;
                    case Frequency.Weekly:
                        reportableEvent = "EOW";
                        break;
                    case Frequency.Daily:
                        reportableEvent = "EOD";
                        break;
                }

                var darValue = new DataAcquisitionRequestedValue()
                {
                    PatientId = patientId,
                    ReportableEvent = reportableEvent,
                    ScheduledReports = new List<ScheduledReport>()
                                {
                                    new ()
                                    {
                                        ReportTrackingId = schedule.Id!,
                                        StartDate = schedule.ReportStartDate,
                                        EndDate = schedule.ReportEndDate,
                                        Frequency = schedule.Frequency,
                                        ReportTypes = schedule.ReportTypes
                                    }
                                },
                    QueryType = QueryType.Initial.ToString(),
                };

                var headers = new Headers
                            {
                                { "X-Correlation-Id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
                            };

                _dataAcqProducer.Produce(nameof(KafkaTopic.DataAcquisitionRequested),
                    new Message<string, DataAcquisitionRequestedValue>
                    { Key = darKey, Value = darValue, Headers = headers });
                _dataAcqProducer.Flush();
            }

            return true;
        }
    }
}
