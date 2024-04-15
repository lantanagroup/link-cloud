using Azure.Identity;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.ExternalServices
{
    public static class ExternalConfigurationExtension
    {
        public static WebApplicationBuilder AddExternalConfiguration(this WebApplicationBuilder builder, Action<ExternalConfigurationOptions>? options = null)
        {
            var externalConfigurationOptions = new ExternalConfigurationOptions();
            options?.Invoke(externalConfigurationOptions);

            if (!string.IsNullOrEmpty(externalConfigurationOptions.ExternalConfigurationSource))
            {
                switch (externalConfigurationOptions.ExternalConfigurationSource)
                {
                    case "AzureAppConfiguration":
                        builder.Configuration.AddAzureAppConfiguration(options =>
                        {
                            options.Connect(externalConfigurationOptions.ExternalConfigurationConnectionString)
                                // Load configuration values with no label
                                .Select("*", LabelFilter.Null)
                                // Load configuration values for service name
                                .Select("*", LinkAdminConstants.ServiceName)
                                // Load configuration values for service name and environment
                                .Select("*", LinkAdminConstants.ServiceName + ":" + externalConfigurationOptions.Environment.EnvironmentName);

                            options.ConfigureKeyVault(kv =>
                            {
                                kv.SetCredential(new DefaultAzureCredential());
                            });

                        });
                        break;
                }
            }

            return builder;
        }
    }

    public class ExternalConfigurationOptions
    {
        public IWebHostEnvironment Environment { get; set; } = null!;
        public string? ExternalConfigurationSource { get; set; }
        public string? ExternalConfigurationConnectionString { get; set; }
    }
}
