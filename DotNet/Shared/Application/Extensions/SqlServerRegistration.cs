using LantanaGroup.Link.Shared.Domain.Repositories.Interceptors;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Extensions;
public static class SqlServerRegistration
{
    public static void AddSQLServerEF<T>(this WebApplicationBuilder builder, bool useUpdateBaseEntityInterceptor = false) where T : DbContext
    {
        builder.Services.AddDbContext<T>((sp, options) =>
        {
            var updateBaseEntityInterceptor = sp.GetService<UpdateBaseEntityInterceptor>();

            switch (builder.Configuration.GetValue<string>(ConfigurationConstants.AppSettings.DatabaseProvider))
            {
                case ConfigurationConstants.AppSettings.SqlServerDatabaseProvider:
                    {
                        if (useUpdateBaseEntityInterceptor)
                        {
                            string? connectionString =
                                builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                                    .DatabaseConnection);

                            if (string.IsNullOrEmpty(connectionString))
                                throw new InvalidOperationException("Database connection string is null or empty.");
                            
                            options.UseSqlServer(connectionString)
                                .AddInterceptors(updateBaseEntityInterceptor);
                        }
                        else
                        {
                            options.UseSqlServer(
                                builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections
                                    .DatabaseConnection));
                        }
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {ConfigurationConstants.AppSettings.DatabaseProvider}");
            }
        });
    }
}
