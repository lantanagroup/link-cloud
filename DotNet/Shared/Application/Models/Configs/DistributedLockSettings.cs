using LantanaGroup.Link.Shared.Settings;
using Medallion.Threading.Redis;
using Medallion.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Models.Configs;
public class DistributedLockSettings
{
    public string? ConnectionString { get; set; } = string.Empty;
    public string? Password { get; set; } = string.Empty;
    public TimeSpan Expiration { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxRetryCount { get; set; } = 3;
}

public static class DistributedLockSettingsExtensions
{
    public static void AddDistributedLockSettingsToContainer(this IServiceCollection services, IConfiguration configuration)
    {
        var distributedLockSettings = configuration.GetSection("DistributedLockSettings").Get<DistributedLockSettings>();
        services.AddSingleton(distributedLockSettings);
    }

    public static DistributedLockSettings BuildDistributedLockSettings(this DistributedLockSettings settings, IServiceCollection services, IConfiguration configuration, string connectionStringKey)
    {
        var connectionString = configuration.GetConnectionString(connectionStringKey);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString), $"Connection string '{connectionStringKey}' is not found in the configuration.");
        }

        settings.ConnectionString = connectionString;
        services.Configure<DistributedLockSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.DistributedLockSettings));

        var pw = configuration.GetValue<string>("Redis:Password"); // Assuming Redis password is stored in configuration
        if(!string.IsNullOrWhiteSpace(pw))
            settings.Password = pw;

        return settings;
    }

    public static void DistributedLockBuildAndAddToDI(IServiceCollection services, IConfiguration configuration, string connectionStringKey)
    {
        //builder.Services.Configure<LinkTokenServiceSettings>(builder.Configuration.GetSection(ConfigurationConstants.AppSettings.LinkTokenService));
        var distributedLockSettings = configuration.GetSection("DistributedLockSettings").Get<DistributedLockSettings>();

        if (distributedLockSettings == null)
        {
            throw new ArgumentNullException(nameof(distributedLockSettings), "DistributedLockSettings section is missing in the configuration.");
        }

        distributedLockSettings =  distributedLockSettings.BuildDistributedLockSettings(services, configuration, connectionStringKey);

        if (string.IsNullOrWhiteSpace(distributedLockSettings.ConnectionString))
        {
            throw new ArgumentNullException(nameof(distributedLockSettings.ConnectionString), "ConnectionString with key of \"Redis\" is required in ConnectionStrings section.");
        }

        //Distributed Semaphore
        var connectionMultiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(new StackExchange.Redis.ConfigurationOptions
        {
            EndPoints = { distributedLockSettings.ConnectionString },
            AbortOnConnectFail = false,
            Password = distributedLockSettings.Password,
        });
        services.AddSingleton<IDistributedSemaphoreProvider>(new RedisDistributedSynchronizationProvider(connectionMultiplexer.GetDatabase()));
    }
}