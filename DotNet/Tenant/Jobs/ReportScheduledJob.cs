﻿using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Interfaces;
using LantanaGroup.Link.Tenant.Models.Messages;
using LantanaGroup.Link.Tenant.Services;
using MongoDB.Driver.Linq;
using Quartz;
using System.Text.Json;

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

                String[] reportTypes = [];

                string trigger = (string)triggerMap[TenantConstants.Scheduler.JobTrigger];

                FacilityConfigModel facility = (FacilityConfigModel)dataMap[TenantConstants.Scheduler.Facility];

                string frequency = (string)dataMap[TenantConstants.Scheduler.Frequency];


                DateTime currentDateInTimeZone = DateTime.UtcNow;

                // initialize startDate, endDate
                DateTime startDate = currentDateInTimeZone;
                DateTime endDate = currentDateInTimeZone;

                // adjust startDate, endDate based on frequency
                switch (frequency)
                {
                    case ScheduleService.MONTHLY:
                        startDate = new DateTime(currentDateInTimeZone.Year, currentDateInTimeZone.Month, 1);
                        endDate = startDate.AddMonths(1).AddSeconds(-1);
                        reportTypes = facility.ScheduledReports.Monthly;
                        break;
                    case ScheduleService.WEEKLY:
                        startDate = new DateTime(currentDateInTimeZone.Year, currentDateInTimeZone.Month, currentDateInTimeZone.Day);
                        // set to beginning of week in case is not exactly that
                        DayOfWeek startOfWeek = DayOfWeek.Sunday;
                        DayOfWeek currentDay = startDate.DayOfWeek;
                        int difference = currentDay - startOfWeek;
                        startDate = startDate.AddDays(-difference);
                        // end date of the week
                        endDate = startDate.AddDays(7).AddSeconds(-1);
                        reportTypes = facility.ScheduledReports.Weekly;
                        break;
                    case ScheduleService.DAILY:
                        startDate = new DateTime(currentDateInTimeZone.Year, currentDateInTimeZone.Month, currentDateInTimeZone.Day);
                        endDate = startDate.AddDays(1).AddSeconds(-1);
                        reportTypes = facility.ScheduledReports.Daily;
                        break;
                }

                _logger.LogInformation($"Produce {KafkaTopic.ReportScheduled} + event for facility {facility.FacilityId} and {frequency} trigger: {trigger}");

                var headers = new Headers();
                string correlationId = Guid.NewGuid().ToString();

                headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                ReportScheduledKey Key = new ReportScheduledKey()
                {
                    FacilityId = facility.FacilityId,
                };

                var message = new Message<string, object>
                {
                    Key = JsonSerializer.Serialize(Key),
                    Headers = headers,
                    Value = new ReportScheduledMessage()
                    {
                        ReportTypes = reportTypes,
                        Frequency = frequency,
                        StartDate = startDate,
                        EndDate = endDate
                    },
                };

                var producerConfig = new ProducerConfig();

                var producer = _kafkaProducerFactory.CreateProducer(producerConfig);

                await producer.ProduceAsync(KafkaTopic.ReportScheduled.ToString(), message);

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
