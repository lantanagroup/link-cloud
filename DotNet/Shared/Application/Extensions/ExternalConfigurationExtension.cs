using Azure.Identity;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Shared.Application.Extensions;

public static class ExternalConfigurationExtension
{
    public static WebApplicationBuilder AddExternalConfiguration(this WebApplicationBuilder builder, string serviceName)
    {
        using var loggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            logging.AddConsole();
            logging.AddDebug();
            logging.AddEventSourceLogger();
        });
        var logger = loggerFactory.CreateLogger("ExternalConfigurationExtension");

        var externalConfigurationSource = builder.Configuration.GetSection(ConfigurationConstants.AppSettings.ExternalConfigurationSource).Get<string>();

        if (externalConfigurationSource is not null)
        {
            logger.LogInformation("External configuration source configured: {ExternalConfigurationSource}.", externalConfigurationSource);

            switch (externalConfigurationSource)
            {
                case "AzureAppConfiguration":
                    builder.Configuration.AddAzureAppConfiguration(options =>
                    {
                        const string patientTagsKey = "Telemetry:PatientTags";
                        const string includeOrgResourceKey = "PatientAggregator:IncludeOrganizationResource";

                        string? connectionString =
                            builder.Configuration.GetConnectionString(ConfigurationConstants.DatabaseConnections.AzureAppConfiguration);

                        if (string.IsNullOrEmpty(connectionString))
                        {
                            logger.LogError(
                                "External configuration source {ExternalConfigurationSource} is configured but connection string {ConnectionStringName} is missing or empty.",
                                externalConfigurationSource,
                                ConfigurationConstants.DatabaseConnections.AzureAppConfiguration);
                            return;
                        }

                        try
                        {
                            logger.LogInformation("Connecting to external configuration source {ExternalConfigurationSource}.", externalConfigurationSource);

                            options.Connect(connectionString)
                                // Load configuration values with no label
                                .Select("*", LabelFilter.Null)
                                // Load configuration values for service name
                                .Select("*", serviceName)
                                // Load configuration values for service name and environment
                                .Select("*",
                                    serviceName + ":" + builder.Environment);

                            options.ConfigureRefresh(refresh =>
                            {
                                refresh.Register(patientTagsKey, refreshAll: true);
                                refresh.Register(includeOrgResourceKey, refreshAll: true);
                            });

                            builder.Services.AddSingleton(options.GetRefresher());
                            builder.Services.AddHostedService<AzureAppConfigurationRefreshService>();

                            options.ConfigureKeyVault(kv => { kv.SetCredential(new DefaultAzureCredential()); });
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex,
                                "External configuration source {ExternalConfigurationSource} is configured but failed to connect.",
                                externalConfigurationSource);
                            throw;
                        }
                    });
                    break;
            }
        }

        return builder;
    }

    public static ServiceInformation SetupServiceInformation(this IHostApplicationBuilder builder, string serviceName, string assemblyVersion)
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
