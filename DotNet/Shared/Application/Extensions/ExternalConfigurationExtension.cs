using Azure.Identity;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    public static ServiceInformation SetupServiceInformation(this IHostApplicationBuilder builder, string serviceName, string assemblyVersion)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.DatabaseConnection);

        return SetupServiceInformation(builder, serviceName, assemblyVersion, connectionString);
    }

    public static ServiceInformation SetupServiceInformation(this IHostApplicationBuilder builder, string serviceName, string assemblyVersion, string? connectionString)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            throw new NullReferenceException("Service Name is required.");
        }

        if (string.IsNullOrEmpty(assemblyVersion))
        {
            throw new NullReferenceException("Assembly Version is required.");
        }

        var serviceInformation = builder.Configuration.GetRequiredSection(ServiceInformation.SectionName).Get<ServiceInformation>();

        if (serviceInformation != null)
        {
            serviceInformation!.ServiceConfigName = serviceName;
            serviceInformation.ConnectionString = connectionString;
            builder.Services.AddSingleton<ServiceInformation>(serviceInformation);
            ServiceActivitySource.Initialize(assemblyVersion, serviceInformation);
        }
        else
        {
            throw new NullReferenceException("Service Information was null.");
        }

        return serviceInformation;
    }
}