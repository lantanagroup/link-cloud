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
                // Use for SqlServer database
                c.UseSqlServer(sqlServerOptions =>
                {
                    sqlServerOptions.UseDriverDelegate<SqlServerDelegate>();
                    sqlServerOptions.ConnectionString = connectionString;
                    sqlServerOptions.TablePrefix = "QRTZ_";
                });
                c.UseSystemTextJsonSerializer();
            });

            q.ScheduleJob<AcquisitionProcessingJob>(trigger => trigger
                .WithIdentity("AcquisitionProcessingJobTrigger", "DataAcquisition")
                .WithIdentity("AcquisitionProcessingJob", "DataAcquisition")
                .WithDescription("Job to process acquisition tasks")
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(30) // Adjust the interval as needed
                    .RepeatForever())
                .StartNow()
                );
        });
    }
}
