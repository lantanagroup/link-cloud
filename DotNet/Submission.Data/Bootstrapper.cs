using LantanaGroup.Link.Shared.Application.Extensions.Quartz;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Submission.Data
{
    public static class Bootstrapper
    {
        public static void AddSubmissionDataServices(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection)
                                        ?? throw new InvalidOperationException("Database connection string is not configured.");

            services.AddDbContext<SubmissionContext>((sp, options) =>
            {                                                            
                options.UseSqlServer(connectionString);
            });
        }
    }
}
