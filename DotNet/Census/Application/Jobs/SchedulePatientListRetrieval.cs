using Census.Domain.Entities;
using Confluent.Kafka;
using LantanaGroup.Link.Census.Application.Settings;
using LantanaGroup.Link.Shared.Application.Extensions;
using LantanaGroup.Link.Shared.Application.Models;
using Quartz;

namespace LantanaGroup.Link.Census.Application.Jobs;

[DisallowConcurrentExecution]
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
        var facility = context.JobDetail.JobDataMap.GetObject<CensusConfigEntity>(CensusConstants.Scheduler.Facility);

        _logger.LogDebug("Triggering {Topic} for facility: {FacilityId}", KafkaTopic.PatientCensusScheduled.ToString(), facility.FacilityID);

        // Skip execution if facility is disabled
        if (facility.Enabled == false)
        {
            _logger.LogInformation("Skipping execution for disabled facility: {FacilityId}", facility.FacilityID);
            return;
        }

        try
        {
            await _kafkaProducer.ProduceAsync(KafkaTopic.PatientCensusScheduled.ToString(), new Message<string, Null>
            {
                Key = facility.FacilityID
            });
        }
        catch (ProduceException<string, Null> ex)
        {
            _logger.LogError(ex, "SchedulePatientListRetrieval: Error producing {Topic} message to Kafka for facility: {FacilityId}", KafkaTopic.PatientCensusScheduled.ToString(), facility.FacilityID);
            throw;
        }
    }
}
