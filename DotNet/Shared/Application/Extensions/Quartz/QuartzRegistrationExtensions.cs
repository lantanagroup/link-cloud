using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Impl.AdoJobStore;
using System.Text.Json.Serialization;
using Quartz.Impl;
using Quartz.Spi;

namespace LantanaGroup.Link.Shared.Application.Extensions.Quartz;
public static class QuartzRegistrationExtensions
{
    public static void RegisterQuartzDatabase(this IServiceCollection collection, string connectionString) {

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
                    sqlServerOptions.TablePrefix = "QRTZ_";
                });
                c.UseSystemTextJsonSerializer();
            });
        });
        //collection.AddQuartzHostedService(x => { x.AwaitApplicationStarted = true; x.WaitForJobsToComplete = true; });
    }
}
