using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Services;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Utilities;
using Quartz;
using static LantanaGroup.Link.Tenant.Services.ScheduleService;

namespace LantanaGroup.Link.Tenant.Jobs
{

    [DisallowConcurrentExecution]
    public class ReportScheduledJob : IJob
    {
        private readonly ILogger<ReportScheduledJob> _logger;
        private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;
        private readonly ITenantServiceMetrics _metrics;

        public ReportScheduledJob(ILogger<ReportScheduledJob> logger, IKafkaProducerFactory<string, object> kafkaProducerFactory, ITenantServiceMetrics metrics)
        {
            _logger = logger;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap dataMap = context.JobDetail.JobDataMap;

                JobDataMap triggerMap = context.Trigger.JobDataMap!;

                string[] reportTypes = [];

                string trigger = (string)triggerMap[TenantConstants.Scheduler.JobTrigger];

                var facility = dataMap.GetObject<Facility>(TenantConstants.Scheduler.Facility);

                string frequency = dataMap.GetObject<string>(TenantConstants.Scheduler.Frequency);

                TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(facility.TimeZone); // based on location

                // Anchor on the scheduled fire time, not the wall clock: a misfire recovered after
                // midnight must still announce the period it was scheduled for.
                DateTimeOffset scheduledUtc = context.ScheduledFireTimeUtc ?? context.FireTimeUtc;
                DateTime currentDateInTimeZone = TimeZoneInfo.ConvertTime(scheduledUtc, timeZone).DateTime;

                reportTypes = frequency switch
                {
                    ScheduleService.MONTHLY => facility.ScheduledReports.Monthly,
                    ScheduleService.WEEKLY => facility.ScheduledReports.Weekly,
                    ScheduleService.DAILY => facility.ScheduledReports.Daily,
                    _ => []
                };

                var (startDate, endDate) = ReportingPeriodMath.ForFrequency(frequency, currentDateInTimeZone, timeZone);

                _logger.LogInformation("Produce {Topic} event on facility time {CurrentDateTime} for facility {FacilityId}, frequency {Frequency}, trigger: {Trigger}", KafkaTopic.ReportScheduled, currentDateInTimeZone, facility.FacilityId, frequency, trigger);

                var headers = new Headers();
                string correlationId = Guid.NewGuid().ToString();

                headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                var message = new Message<string, object>
                {
                    Key = facility.FacilityId,
                    Headers = headers,
                    Value = new ReportScheduledMessage()
                    {
                        ReportTypes = reportTypes,
                        Frequency = frequency,
                        StartDate = startDate,
                        EndDate = endDate,
                        ReportTrackingId = correlationId
                    },
                };

                var producerConfig = new ProducerConfig();

                var producer = _kafkaProducerFactory.CreateProducer(producerConfig);

                try
                {
                    await producer.ProduceAsync(KafkaTopic.ReportScheduled.ToString(), message);
                }
                catch (ProduceException<string, ReportScheduledMessage> ex)
                {
                    _logger.LogError(ex, "An error was encountered generating a ReportScheduled event.\n\tFacilityId: {facilityId}\n\tReportTypes: {reportTypes}", facility.FacilityId, string.Join(',', reportTypes));
                }

                _metrics.IncrementReportScheduledCounter([
                    new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facility.FacilityId),
                    new KeyValuePair<string, object?>(DiagnosticNames.ReportType, reportTypes),
                    new KeyValuePair<string, object?>(DiagnosticNames.PeriodStart, startDate),
                    new KeyValuePair<string, object?>(DiagnosticNames.PeriodEnd, endDate)
                ]);

            }
            catch (Exception ex)
            {
                Thread.Sleep(600000); //sleep for 10 mins

                JobExecutionException e2 = new JobExecutionException(ex);
                //fire it again
                object value = e2.RefireImmediately;
                throw e2;
            }
        }
    }
}
