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
        var connectionString = BuildRedisConfiguration(configuration);
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
                var options = ConfigurationOptions.Parse(connectionString);
                options.AbortOnConnectFail = false;
                if (options.ConnectTimeout <= 0 || options.ConnectTimeout > 2000)
                    options.ConnectTimeout = 2000;
                if (options.AsyncTimeout <= 0 || options.AsyncTimeout > 2000)
                    options.AsyncTimeout = 2000;
                var multiplexer = ConnectionMultiplexer.Connect(options);
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

    /// <summary>
    /// Compose Redis is password-protected. ConnectionStrings:Redis usually has the host only;
    /// the password lives on Redis:Password or ResourceCache:Redis:Password.
    /// </summary>
    public static string? BuildRedisConfiguration(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.RedisConnection);
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = configuration["ResourceCache:Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var password = configuration[ConfigurationConstants.AppSettings.RedisPassword]
            ?? configuration["ResourceCache:Redis:Password"]
            ?? configuration["REDIS_PASS"];
        return ApplyRedisPassword(connectionString, password);
    }

    public static string ApplyRedisPassword(string connectionString, string? password)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        if (!string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(options.Password))
            options.Password = password;
        return options.ToString(includePassword: true);
    }
}
