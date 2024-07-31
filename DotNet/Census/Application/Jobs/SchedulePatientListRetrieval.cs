using Census.Domain.Entities;
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Jobs;

public class SchedulePatientListRetrieval : IJob
{
    private readonly ILogger<SchedulePatientListRetrieval> _logger;
    private readonly IProducer<string, Null> _kafkaProducer;

    public SchedulePatientListRetrieval(ILogger<SchedulePatientListRetrieval> logger, IProducer<string, Null> kafkaProducer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        //get facility
        var facility = (CensusConfigEntity)context.JobDetail.JobDataMap.Get(CensusConstants.Scheduler.Facility);
        _logger.LogInformation($"Triggering {KafkaTopic.PatientCensusScheduled.ToString()} for facility: {facility.FacilityID} ");

        await _kafkaProducer.ProduceAsync(KafkaTopic.PatientCensusScheduled.ToString(), new Message<string, Null>
        {
            Key = facility.FacilityID
        });
    }
}
