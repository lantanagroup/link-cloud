using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Tenant.Config;
using LantanaGroup.Link.Tenant.Models.Messages;
using Quartz;

namespace LantanaGroup.Link.Tenant.Jobs
{

    [DisallowConcurrentExecution]
    public class RetentionCheckScheduledJob : IJob
    {
        private readonly ILogger<RetentionCheckScheduledJob> _logger;
        private readonly IKafkaProducerFactory<string, object> _kafkaProducerFactory;

        public RetentionCheckScheduledJob(ILogger<RetentionCheckScheduledJob> logger, IKafkaProducerFactory<string, object> kafkaProducerFactory)
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

                string tenant = (string)dataMap[TenantConstants.Scheduler.JobName];

                _logger.LogInformation(" RetentionCheckScheduledJob -  Produce event for:  {Tenant} and crontrigger: {Trigger}", tenant, trigger);

                var headers = new Headers();

                string correlationId = Guid.NewGuid().ToString();

                headers.Add("X-Correlation-Id", System.Text.Encoding.ASCII.GetBytes(correlationId));

                var producerConfig = new ProducerConfig();

                var producer = _kafkaProducerFactory.CreateProducer(producerConfig);

                var message = new Message<string, object>
                {
                    Key = tenant,
                    Headers = headers,
                    Value = new RetentionCheckScheduledMessage()
                    {
                        TenantId = tenant,
                    }
                };

                await producer.ProduceAsync(KafkaTopic.RetentionCheckScheduled.ToString(), message);

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
