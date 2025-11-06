using Azure.Identity;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class ExternalConfigurationExtension
{
    public static WebApplicationBuilder AddExternalConfiguration(this WebApplicationBuilder builder, string serviceName)
    {
        var externalConfigurationSource = builder.Configuration.GetSection(ConfigurationConstants.AppSettings.ExternalConfigurationSource).Get<string>();

        if (externalConfigurationSource is not null)
        {
            switch (externalConfigurationSource)
            {
                case "AzureAppConfiguration":
                    builder.Configuration.AddAzureAppConfiguration(options =>
                    {
                        string? connectionString =
                            builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.AzureAppConfiguration);
                        
                        if (!string.IsNullOrEmpty(connectionString))
                        {
                            options.Connect(connectionString)
                                // Load configuration values with no label
                                .Select("*", LabelFilter.Null)
                                // Load configuration values for service name
                                .Select("*", serviceName)
                                // Load configuration values for service name and environment
                                .Select("*",
                                    serviceName + ":" + builder.Environment);

                            options.ConfigureKeyVault(kv => { kv.SetCredential(new DefaultAzureCredential()); });
                        }
                    });
                    break;
            }
        }

        return builder;
    }
}