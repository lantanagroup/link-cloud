using AngleSharp.Dom;
using LanatanGroup.Link.QueryDispatch.Jobs;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Spi;

namespace LantanaGroup.Link.QueryDispatch.Presentation.Services
{
    public class ScheduleService : IHostedService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
        private readonly ILogger<ScheduleService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        private static Dictionary<string, Type> _topicJobs = new Dictionary<string, Type>();

        static ScheduleService()
        {
            _topicJobs.Add(KafkaTopic.ReportScheduled.ToString(), typeof(QueryDispatchJob));
        }

        public ScheduleService(
            ISchedulerFactory schedulerFactory,
            IJobFactory jobFactory,
            ILogger<ScheduleService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _schedulerFactory = schedulerFactory;
            _jobFactory = jobFactory;
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public IScheduler Scheduler { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var queryDispatchConfigurationRepo = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<QueryDispatchConfigurationEntity>>();

                var queryPatientDispatchRepo = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<PatientDispatchEntity>>();

                Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                Scheduler.JobFactory = _jobFactory;

                List<QueryDispatchConfigurationEntity> configs = await queryDispatchConfigurationRepo.GetAllAsync(cancellationToken);

                List<PatientDispatchEntity> patientDispatches = await queryPatientDispatchRepo.GetAllAsync(cancellationToken);

                // Get all unique facility IDs from configs and dispatches
                var allFacilityIds = configs.Select(c => c.FacilityId)
                    .Union(patientDispatches.Select(p => p.FacilityId))
                    .ToList();

                string group = nameof(KafkaTopic.PatientEvent);

                // Ensure jobs exist for all facilities
                foreach (var facilityId in allFacilityIds)
                {
                    JobKey jobKey = new JobKey(facilityId, group);
                    if (!await Scheduler.CheckExists(jobKey))
                    {
                        IJobDetail job = CreateJob(facilityId);
                        await Scheduler.AddJob(job, true);
                    }
                }

                // Group dispatches by facility
                var dispatchesByFacility = patientDispatches.GroupBy(p => p.FacilityId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Sync triggers for each facility
                foreach (var facilityId in allFacilityIds)
                {
                    JobKey jobKey = new JobKey(facilityId, group);
                    var existingTriggers = await Scheduler.GetTriggersOfJob(jobKey);

                    // Clean orphan triggers
                    foreach (var trigger in existingTriggers)
                    {
                        string patientId = trigger.Description;
                        DateTimeOffset startTimeUtc = trigger.StartTimeUtc;

                        var matchingDispatch = dispatchesByFacility.GetValueOrDefault(facilityId)?
                            .FirstOrDefault(d => d.PatientId == patientId && ComputeOffset(d).UtcDateTime == startTimeUtc.UtcDateTime);

                        if (matchingDispatch == null)
                        {
                            await Scheduler.UnscheduleJob(trigger.Key);
                            _logger.LogInformation("Removed orphan trigger for patient {PatientId} in facility {FacilityId}.", patientId, facilityId);
                        }
                    }

                    // Add missing triggers
                    if (dispatchesByFacility.TryGetValue(facilityId, out var dispatches))
                    {
                        foreach (var dispatch in dispatches)
                        {
                            DateTimeOffset expectedStart = ComputeOffset(dispatch);

                            var matchingTrigger = existingTriggers.FirstOrDefault(t =>
                                t.Description == dispatch.PatientId && t.StartTimeUtc.UtcDateTime == expectedStart.UtcDateTime);

                            if (matchingTrigger == null)
                            {
                                ITrigger trigger = CreateTrigger(dispatch, jobKey);
                                await Scheduler.ScheduleJob(trigger);
                                _logger.LogInformation("Added trigger for patient in facility {FacilityId}.", facilityId);
                            }
                        }
                    }
                }

                // Clean up orphan jobs (facilities no longer present, with no triggers)
                var matcher = GroupMatcher<JobKey>.GroupEquals(group);
                var allJobKeys = await Scheduler.GetJobKeys(matcher);
                foreach (var jobKey in allJobKeys)
                {
                    if (!allFacilityIds.Contains(jobKey.Name))
                    {
                        var triggers = await Scheduler.GetTriggersOfJob(jobKey);
                        if (triggers.Count == 0)
                        {
                            await Scheduler.DeleteJob(jobKey);
                            _logger.LogInformation("Cleaned up orphan job for removed facility: {FacilityId}.", jobKey.Name);
                        }
                        else
                        {
                            _logger.LogWarning("Orphan job for removed facility {FacilityId} has pending triggers, not deleting.", jobKey.Name);
                        }
                    }
                }

                await Scheduler.Start(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start quartz schedule");
                throw new ApplicationException("Failed to start quartz schedule");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler?.Shutdown(cancellationToken);
        }

        public static async Task CreateJobAndTrigger(PatientDispatchEntity patientDispatch, IScheduler scheduler)
        {
            string group = nameof(KafkaTopic.PatientEvent);
            JobKey jobKey = new JobKey(patientDispatch.FacilityId, group);

            if (!await scheduler.CheckExists(jobKey))
            {
                IJobDetail job = CreateJob(patientDispatch.FacilityId);
                await scheduler.AddJob(job, true);
            }

            var existingTriggers = await scheduler.GetTriggersOfJob(jobKey);
            DateTimeOffset expectedStart = ComputeOffset(patientDispatch);

            var matchingTrigger = existingTriggers.FirstOrDefault(t =>
                t.Description == patientDispatch.PatientId && t.StartTimeUtc.UtcDateTime == expectedStart.UtcDateTime);

            if (matchingTrigger == null)
            {
                ITrigger trigger = CreateTrigger(patientDispatch, jobKey);
                await scheduler.ScheduleJob(trigger);
            }
        }

        public static async Task DeleteJob(string facilityId, IScheduler scheduler)
        {
            string group = nameof(KafkaTopic.PatientEvent);
            JobKey jobKey = new JobKey(facilityId, group);

            if (await scheduler.CheckExists(jobKey))
            {
                await scheduler.DeleteJob(jobKey);
            }
        }

        private static IJobDetail CreateJob(string facilityId)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put("FacilityId", facilityId);

            return JobBuilder
                .Create(typeof(QueryDispatchJob))
                .StoreDurably()
                .WithIdentity(facilityId, nameof(KafkaTopic.PatientEvent))
                .WithDescription($"{facilityId}-{nameof(KafkaTopic.PatientEvent)}")
                .UsingJobData(jobDataMap)
                .Build();
        }

        private static ITrigger CreateTrigger(PatientDispatchEntity patientDispatchEntity, JobKey jobKey)
        {
            JobDataMap jobDataMap = new JobDataMap();

            jobDataMap.Put("PatientDispatchEntity", patientDispatchEntity);

            var offset = ComputeOffset(patientDispatchEntity);

            return TriggerBuilder
                .Create()
                .StartAt(offset)
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithDescription(patientDispatchEntity.PatientId)
                .UsingJobData(jobDataMap)
                .Build();
        }

        private static DateTimeOffset ComputeOffset(PatientDispatchEntity entity)
        {
            var dt = entity.TriggerDate;
            var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            return DateBuilder.DateOf(local.Hour, local.Minute, local.Second, local.Day, local.Month, local.Year);
        }
    }
}