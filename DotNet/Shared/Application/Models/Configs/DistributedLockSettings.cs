using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Shared.Application.Models.Configs;
public class DistributedLockSettings
{
    public string ConnectionString { get; set; } = string.Empty;
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
    }
}