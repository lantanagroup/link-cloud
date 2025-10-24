using LantanaGroup.Link.Shared.Application.Models;
using LanatanGroup.Link.QueryDispatch.Jobs;
using Quartz;
using Quartz.Spi;
using LantanaGroup.Link.QueryDispatch.Domain.Entities;
using LantanaGroup.Link.QueryDispatch.Application.Interfaces;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

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
                // var _getAllQueryDispatchConfigurationQuery = scope.ServiceProvider.GetRequiredService<IGetAllQueryDispatchConfigurationQuery>();
                var queryDispatchConfigurationRepo = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<QueryDispatchConfigurationEntity>>();

                var queryPatientDispatchRepo = scope.ServiceProvider.GetRequiredService<IBaseEntityRepository<PatientDispatchEntity>>();

                Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                Scheduler.JobFactory = _jobFactory;

                //List<QueryDispatchConfigurationEntity> configs = await _getAllQueryDispatchConfigurationQuery.Execute();
                List<QueryDispatchConfigurationEntity> configs  = await queryDispatchConfigurationRepo.GetAllAsync(cancellationToken);

                foreach (var config in configs)
                {
                    var job = CreateJob(config.FacilityId);
                    await Scheduler.AddJob(job, true);
                }

               // List<PatientDispatchEntity> patientDispatches = await _getAllPatientDispatchQuery.Execute();

                List<PatientDispatchEntity> patientDispatches = await queryPatientDispatchRepo.GetAllAsync(cancellationToken);

                foreach (var patientDispatch in patientDispatches)
                {
                    JobKey jobKey = new JobKey(patientDispatch.FacilityId)
                    {
                        Group = nameof(KafkaTopic.PatientEvent)
                    };

                    IJobDetail job = await Scheduler.GetJobDetail(jobKey);

                    //NOTE: Converting back to local time for trigger...This feels wrong. Maybe there's a way to have the trigger set by UTC
                    patientDispatch.TriggerDate = patientDispatch.TriggerDate.ToLocalTime();

                    var trigger = CreateTrigger(patientDispatch, job.Key);
                    await Scheduler.ScheduleJob(trigger);
                }

                await Scheduler.Start(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start quartz schedule");
                throw new ApplicationException($"Failed to start quartz schedule");
            }
        }


        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler?.Shutdown(cancellationToken);
        }

        public static async Task CreateJobAndTrigger(PatientDispatchEntity patientDispatch, IScheduler scheduler)
        {
            JobKey jobKey = new JobKey(patientDispatch.FacilityId)
            {
                Group = nameof(KafkaTopic.PatientEvent)
            };

            IJobDetail? job = await scheduler.GetJobDetail(jobKey);

            if (job == null)
            {
                job = CreateJob(patientDispatch.FacilityId);
                await scheduler.AddJob(job, true);
            }

            ITrigger trigger = CreateTrigger(patientDispatch, job.Key);
            await scheduler.ScheduleJob(trigger);
        }

        public static async Task DeleteJob(string facilityId, IScheduler scheduler)
        {
            JobKey jobKey = new JobKey(facilityId)
            {
                Group = nameof(KafkaTopic.PatientEvent)
            };

            IJobDetail job = await scheduler.GetJobDetail(jobKey);

            if (job != null)
            {
                await scheduler.DeleteJob(job.Key);
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

            var offset = DateBuilder.DateOf(patientDispatchEntity.TriggerDate.Hour, patientDispatchEntity.TriggerDate.Minute, patientDispatchEntity.TriggerDate.Second, patientDispatchEntity.TriggerDate.Day, patientDispatchEntity.TriggerDate.Month);

            return TriggerBuilder
                .Create()
                .StartAt(offset)
                .ForJob(jobKey)
                .WithIdentity(Guid.NewGuid().ToString(), jobKey.Group)
                .WithDescription(patientDispatchEntity.PatientId)
                .UsingJobData(jobDataMap)
                .Build();
        }
    }
}
