using LantanaGroup.Link.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using System.Collections.Specialized;

namespace Submission.Data
{
    public static class Bootstrapper
    {
        public static void AddSubmissionDataServices(this IServiceCollection services, IConfiguration configuration)
        {
            var databaseProvider = configuration.GetValue<string>(ConfigurationConstants.AppSettings.DatabaseProvider);

            services.AddDbContext<SubmissionContext>((sp, options) =>
            {
                var connectionString = configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection)
                            ?? throw new InvalidOperationException("Database connection string is not configured.");     
                
                options.UseSqlServer(connectionString);
            });

            // Main Quartz props for persistent ADOJobStore (as before)
            var quartzProps = new NameValueCollection
            {
                ["quartz.scheduler.instanceName"] = "SubmissionScheduler",
                ["quartz.scheduler.instanceId"] = "AUTO",
                ["quartz.jobStore.clustered"] = "true",
                ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
                ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.SqlServerDelegate, Quartz",
                ["quartz.jobStore.tablePrefix"] = "quartz.QRTZ_",
                ["quartz.jobStore.dataSource"] = "default",
                ["quartz.dataSource.default.connectionString"] = configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection),
                ["quartz.dataSource.default.provider"] = "SqlServer",
                ["quartz.threadPool.type"] = "Quartz.Simpl.SimpleThreadPool, Quartz",
                ["quartz.threadPool.threadCount"] = "5",
                ["quartz.jobStore.useProperties"] = "false",
                ["quartz.serializer.type"] = "json"
            };

            // Register main persistent scheduler factory
            services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory(quartzProps));
            services.AddKeyedSingleton(ConfigurationConstants.RunTimeConstants.RetrySchedulerKeyedSingleton, (provider, key) => provider.GetRequiredService<ISchedulerFactory>());
        }
    }
}
