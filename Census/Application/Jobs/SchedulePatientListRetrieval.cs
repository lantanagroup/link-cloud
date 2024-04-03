using Census.Domain.Entities;
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Wrappers;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Jobs;

public class SchedulePatientListRetrieval : IJob
{
    private readonly ILogger<SchedulePatientListRetrieval> _logger;
    private readonly IKafkaProducerFactory<string, Null> _kafkaFactory;

    public SchedulePatientListRetrieval(ILogger<SchedulePatientListRetrieval> logger, IKafkaProducerFactory<string, Null> kafkaFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaFactory = kafkaFactory ?? throw new ArgumentNullException(nameof(kafkaFactory));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var producerConfig = new ProducerConfig();
        using var producer = _kafkaFactory.CreateProducer(producerConfig);
        //get facility
        var facility = (CensusConfigEntity)context.JobDetail.JobDataMap.Get(CensusConstants.Scheduler.Facility);
        _logger.LogInformation($"Triggering {KafkaTopic.PatientCensusScheduled.ToString()} for facility: {facility.FacilityID} ");

        await producer.ProduceAsync(KafkaTopic.PatientCensusScheduled.ToString(), new Message<string, Null>
        {
            Key = facility.FacilityID
        });
    }
}
