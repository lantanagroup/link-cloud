using LantanaGroup.Link.Shared.Application.Repositories.Interceptors;
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
                case "SqlServer":
                    {
                        if (useUpdateBaseEntityInterceptor)
                            options.UseSqlServer(
                                builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection))
                                .AddInterceptors(updateBaseEntityInterceptor);
                        else
                            options.UseSqlServer(
                                builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection));
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Database provider not supported. Attempting to find section named: {ConfigurationConstants.AppSettings.DatabaseProvider}");
            }
        });
    }
}
