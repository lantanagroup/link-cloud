using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LantanaGroup.Link.Submission.Application.Config;

/// <summary>
/// Registers the internal and external blob storage settings Submission reads and submits payloads with.
/// </summary>
public static class BlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Binds both blob storage sections and stops the host from starting without either connection string.
    /// </summary>
    public static IServiceCollection AddBlobStorageSettings(this IServiceCollection services,
                                                            IConfiguration configuration)
    {
        // Without a connection string the service starts and only fails on its first submission, which for a
        // monthly report can be weeks after the deploy. Fail at startup instead.
        services.AddOptions<InternalBlobStorageSettings>()
            .Bind(configuration.GetSection(InternalBlobStorageSettings.Key))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConnectionString),
                      $"{InternalBlobStorageSettings.Key}:ConnectionString is required.")
            .ValidateOnStart();

        services.AddOptions<ExternalBlobStorageSettings>()
            .Bind(configuration.GetSection(ExternalBlobStorageSettings.Key))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConnectionString),
                      $"{ExternalBlobStorageSettings.Key}:ConnectionString is required.")
            .ValidateOnStart();

        return services;
    }
}
