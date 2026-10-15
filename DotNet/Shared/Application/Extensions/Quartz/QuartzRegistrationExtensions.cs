using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.AdoJobStore;

namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;

public static class QuartzRegistrationExtensions
{
    public static void RegisterQuartzDatabase(this IServiceCollection collection, string? connectionString)
    {

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
        }

        collection.AddQuartz(q =>
        {
            // Clustered job stores need a distinct instance id per node so the cluster manager can tell
            // nodes apart. Without this every process boots as "NON_CLUSTERED": a restart of any pod
            // recovers in-flight firings owned by live peers and jobs can run twice. "AUTO" yields a
            // per-process id (host name + start ticks); the scheduler name stays shared so all nodes
            // still form one cluster.
            q.SchedulerId = StdSchedulerFactory.AutoGenerateInstanceId;

            q.UsePersistentStore(c =>
            {
                // Use for SqlServer database
                c.UseSqlServer(sqlServerOptions =>
                {
                    sqlServerOptions.UseDriverDelegate<SqlServerDelegate>();
                    sqlServerOptions.ConnectionString = connectionString;
                    sqlServerOptions.TablePrefix = "quartz.QRTZ_";
                });
                c.UseSystemTextJsonSerializer();
                c.UseClustering();
            });
        });
    }

    public static void RegisterQuartzDatabaseInTest(this IServiceCollection collection)
    {
        collection.AddQuartz(q =>
        {
            q.UseInMemoryStore();
        });
    }
}
