using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class PipelineAbortRegistryExtensions
{
    public static IServiceCollection AddPipelineAbortRegistry(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveRedisConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.TryAddSingleton<IPipelineAbortRegistry, InMemoryPipelineAbortRegistry>();
            return services;
        }

        services.TryAddSingleton<IPipelineAbortRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisPipelineAbortRegistry>>();
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(connectionString);
                return new RedisPipelineAbortRegistry(multiplexer, logger);
            }
            catch (Exception ex)
            {
                sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("PipelineAbortRegistry")
                    .LogWarning(ex, "Pipeline abort registry could not connect to Redis; abort flags will be process-local.");
                return new InMemoryPipelineAbortRegistry();
            }
        });

        return services;
    }

    private static string? ResolveRedisConnectionString(IConfiguration configuration)
    {
        var fromConnectionStrings = configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.RedisConnection);
        if (!string.IsNullOrWhiteSpace(fromConnectionStrings))
            return fromConnectionStrings;

        return configuration["ResourceCache:Redis:ConnectionString"];
    }
}
