using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;
public static class QuartzRegistrationExtensions
{
    public static void RegisterQuartzDatabase(this IServiceCollection collection, string? connectionString) {

        if(string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString), "Connection string cannot be null or empty.");
        }

        collection.AddQuartz(q =>
        {
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
            });
        });
    }
}

public static class JobDataExtensions
{
    public static T GetJobDataValue<T>(this IJobExecutionContext context, string key)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentNullException(nameof(key));
        }
        JobDataMap dataMap = context.JobDetail.JobDataMap;
        if (dataMap.ContainsKey(key))
        {
            return (T)dataMap.Get(key);
        }
        else
        {
            throw new KeyNotFoundException($"The key '{key}' was not found in the JobDataMap.");
        }
    }
}
