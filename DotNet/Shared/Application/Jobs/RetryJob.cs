using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text;

namespace LantanaGroup.Link.Shared.Jobs;

[DisallowConcurrentExecution]
public class RetryJob : IJob
{
    private readonly ILogger _logger; private readonly IKafkaProducerFactory<string, string> _retryKafkaProducerFactory; private readonly ISchedulerFactory _schedulerFactory; private readonly IServiceScopeFactory _serviceScopeFactory;

    public RetryJob(
        ILogger<RetryJob> logger,
        IKafkaProducerFactory<string, string> retryKafkaProducerFactory,
        ISchedulerFactory schedulerFactory,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _retryKafkaProducerFactory = retryKafkaProducerFactory;
        _schedulerFactory = schedulerFactory;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var triggerMap = context.Trigger.JobDataMap;
            var retryModel = (RetryModel)triggerMap["RetryModel"];

            _logger.LogInformation("Executing RetryJob for {Topic}-{Id}", retryModel.Topic, retryModel.Id);

            ProducerConfig config = new ProducerConfig()
            {
                CompressionType = CompressionType.Zstd
            };

            Headers headers = new Headers();

            foreach (var header in retryModel.Headers)
            {
                _logger.LogInformation("RetryJob: Logging Message Headers: {key} - {value}", header.Key, Encoding.UTF8.GetBytes(header.Value));
                headers.Add(header.Key, Encoding.UTF8.GetBytes(header.Value));
            }

            using (var producer = _retryKafkaProducerFactory.CreateProducer(config, useOpenTelemetry: false))
            {
                var darKey = retryModel.Key;
                var darValue = retryModel.Value;

                producer.Produce(retryModel.Topic,
                    new Message<string, string>
                    {
                        Key = darKey,
                        Value = darValue,
                        Headers = headers
                    });

                producer.Flush();
            }

            // remove the job from the scheduler
            await RetryScheduleService.DeleteJob(retryModel, await _schedulerFactory.GetScheduler());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encountered in GenerateDataAcquisitionRequestsForPatientsToQuery: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            throw;
        }
    }
}