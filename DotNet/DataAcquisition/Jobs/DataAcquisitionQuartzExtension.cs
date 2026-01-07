using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Jobs;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace LantanaGroup.Link.DataAcquisition.Jobs;
public static class DataAcquisitionQuartzExtension
{
    public static void RegisterQuartzAcquisitionJob(this WebApplicationBuilder builder, string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
        }

        // base configuration from appsettings.json
        builder.Services.Configure<QuartzOptions>(builder.Configuration.GetSection("Quartz"));

        // if you are using persistent job store, you might want to alter some options
        builder.Services.Configure<QuartzOptions>(options =>
        {
            options.Scheduling.IgnoreDuplicates = true; // default: false
            options.Scheduling.OverWriteExistingData = true; // default: true
        });

        builder.Services.AddQuartz(q =>
        {
            
            // handy when part of cluster or you want to otherwise identify multiple schedulers
            q.SchedulerId = "Scheduler-DataAcquisition";
            q.SchedulerName = "DataAcquisitionScheduler";
            
            q.UsePersistentStore(c =>
            {
                //c.UseProperties = true; // Use properties for job data
                // Use for SqlServer database
                c.UseSqlServer(sqlServerOptions =>
                {
                    sqlServerOptions.UseDriverDelegate<SqlServerDelegate>();
                    sqlServerOptions.ConnectionString = connectionString;
                    sqlServerOptions.TablePrefix = "quartz.QRTZ_";
                });
                c.UseSystemTextJsonSerializer();
                c.UseClustering(options =>
                {
                    options.CheckinInterval = TimeSpan.FromSeconds(10); // Adjust as needed
                });
            });
        });

        builder.Services.AddTransient<AcquisitionProcessingJob>();
        
        builder.Services.Configure<QuartzOptions>(options =>
        {
            var jobKey = new JobKey("AcquisitionProcessingJob", "DataAcquisitionGroup");
            options.AddJob<AcquisitionProcessingJob>(j => j.WithIdentity(jobKey).StoreDurably());
            options.AddTrigger(trigger => trigger
                .WithIdentity("AcquisitionProcessingJobTrigger", "DataAcquisitionGroup")
                .ForJob(jobKey)
                .WithSimpleSchedule(schedule => schedule
                    .WithInterval(TimeSpan.FromSeconds(30)) // Adjust the interval as needed
                    .RepeatForever()));

            var retryJobKey = new JobKey("RetryJob", "DataAcquisitionGroup");
            options.AddJob<RetryJob>(j => j.WithIdentity(retryJobKey).StoreDurably());
        });

        //Factories - Retry
        builder.Services.AddTransient<IRetryModelFactory, RetryModelFactory>();

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
    }
}
