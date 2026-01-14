using LantanaGroup.Link.Shared.Settings;
using Medallion.Threading.Redis;
using Medallion.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security;

namespace LantanaGroup.Link.Shared.Application.Models.Configs;
public class DistributedLockSettings
{
    public string? ConnectionString { get; set; } = string.Empty;
    public SecureString? Password { get; set; }
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
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings), "DistributedLockSettings section is missing in the configuration.");
        }

        var connectionString = configuration.GetConnectionString(connectionStringKey);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString), $"Connection string '{connectionStringKey}' is not found in the configuration.");
        }

        settings.ConnectionString = connectionString;
        services.Configure<DistributedLockSettings>(configuration.GetSection(ConfigurationConstants.AppSettings.DistributedLockSettings));

        var pw = configuration.GetValue<string>(ConfigurationConstants.AppSettings.RedisPassword); // Use string for password retrieval

        try
        {
            if (!string.IsNullOrWhiteSpace(pw))
            {
                var securePw = new System.Security.SecureString();
                foreach (var c in pw)
                    securePw.AppendChar(c);
                settings.Password = securePw;
            }
        }
        finally
        {
            //clear plain text password from memory
            pw = null;
        }

        return settings;
    }

    public static void DistributedLockBuildAndAddToDI(IServiceCollection services, IConfiguration configuration, string connectionStringKey)
    {
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
        var configOptions = new StackExchange.Redis.ConfigurationOptions
        {
            EndPoints = { distributedLockSettings.ConnectionString },
            AbortOnConnectFail = false,
        };

        if (distributedLockSettings?.Password != null)
        {
            // Convert SecureString to plain string for Redis password
            var passwordBSTR = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(distributedLockSettings.Password);
            try
            {
                configOptions.Password = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(passwordBSTR);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(passwordBSTR);
            }
        }

        var connectionMultiplexer = StackExchange.Redis.ConnectionMultiplexer.Connect(configOptions);
        services.AddSingleton<IDistributedSemaphoreProvider>(new RedisDistributedSynchronizationProvider(connectionMultiplexer.GetDatabase()));
    }
}