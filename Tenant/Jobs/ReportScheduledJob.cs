using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Entities;
using LantanaGroup.Link.Tenant.Models.Messages;
using Quartz;
using System.Text.Json;

namespace LantanaGroup.Link.Tenant.Jobs
{

    [DisallowConcurrentExecution]
    public class ReportScheduledJob : IJob
    {
        private readonly ILogger<ReportScheduledJob> _logger;
        private readonly IKafkaProducerFactory<string,object> _kafkaProducerFactory;

        public ReportScheduledJob(ILogger<ReportScheduledJob> logger, IKafkaProducerFactory<string,object> kafkaProducerFactory)
        {
            _logger = logger;
            _kafkaProducerFactory = kafkaProducerFactory ?? throw new ArgumentNullException(nameof(kafkaProducerFactory));
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                JobDataMap dataMap = context.JobDetail.JobDataMap;

                JobDataMap triggerMap = context.Trigger.JobDataMap!;

                string trigger = (string)triggerMap[TenantConstants.Scheduler.JobTrigger];

                FacilityConfigModel facility = (FacilityConfigModel)dataMap[TenantConstants.Scheduler.Facility];

                string reportType = (string)dataMap[TenantConstants.Scheduler.ReportType];

                List<KeyValuePair<string, object>> parameters = new List<KeyValuePair<string, object>>();

                /*   DateTime startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0);

                   DateTime endDate1 = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0);

                   DateTime endDate = endDate1.AddMinutes(2);
                */

                DateTime startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

                DateTime endDate = startDate.AddMonths(1).AddSeconds(-1);

                parameters.Add(new KeyValuePair<string, Object>(TenantConstants.Scheduler.StartDate, startDate));

                parameters.Add(new KeyValuePair<string, Object>(TenantConstants.Scheduler.EndDate, endDate));

                _logger.LogInformation($"Produce {KafkaTopic.ReportScheduled} + event for facility {facility.FacilityId} and {reportType} and trigger: {trigger}");

                var headers = new Headers();
                string correlationId = Guid.NewGuid().ToString();

                headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                ReportScheduledKey Key = new ReportScheduledKey()
                {
                    FacilityId = facility.FacilityId,
                    ReportType = reportType
                };

                var message = new Message<string, object>
                {
                    Key = JsonSerializer.Serialize(Key),
                    Headers = headers,
                    Value = new ReportScheduledMessage()
                    {
                        Parameters = parameters
                    },
                };

                var producerConfig = new ProducerConfig();

                var producer = _kafkaProducerFactory.CreateProducer(producerConfig);

                await producer.ProduceAsync(KafkaTopic.ReportScheduled.ToString(), message);
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
