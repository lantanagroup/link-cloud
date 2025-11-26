using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.Models;

public class ServiceInformation
{
    private static readonly ILogger _logger;

    public static string SectionName = "ServiceInformation";
    public string ServiceName { get; init; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ProductVersion { get; init; } = string.Empty;
    public string Commit { get; init; } = string.Empty;
    public string Build { get; init; } = string.Empty;
    public string SwaggerUrl { get; set; } = string.Empty;

    public static ServiceInformation GetServiceInformation(Assembly assembly, IConfiguration configuration)
    {
        var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var serviceInformation = configuration
            .GetRequiredSection(SectionName)
            .Get<ServiceInformation>()!;
        
        if (string.IsNullOrEmpty(serviceInformation.Version))
            serviceInformation.Version = assemblyVersion;
        
        var enableSwagger = configuration.GetValue<bool>("EnableSwagger");

        if (enableSwagger)
            serviceInformation.SwaggerUrl = "/swagger/index.html";

        return serviceInformation;
    }

    /**
     * Gets service information for a given service at its base URL.
     */
    public static async Task<ServiceInformation?> GetServiceInformation(HttpClient client, string? internalBaseUrl, string? externalBaseUrl, string? serviceInfoPath, ILogger logger)
    {
        if (string.IsNullOrEmpty(internalBaseUrl) || string.IsNullOrEmpty(serviceInfoPath))
            return null;

        string serviceInfoEndpoint =
            internalBaseUrl + (serviceInfoPath.StartsWith("/") ? serviceInfoPath : "/" + serviceInfoPath);
        
        try
        {
            var response = await client.GetAsync(serviceInfoEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to retrieve service information from endpoint {Endpoint} due to status code {StatusCode}", serviceInfoEndpoint, response.StatusCode);
                return null;
            }
            var content = await response.Content.ReadAsStringAsync();
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            ServiceInformation? serviceInfo = JsonSerializer.Deserialize<ServiceInformation>(content, options);

            if (serviceInfo != null && !string.IsNullOrEmpty(serviceInfo.SwaggerUrl) &&
                serviceInfo.SwaggerUrl.StartsWith("/"))
            {
                string baseUrl = externalBaseUrl ?? internalBaseUrl;
                
                logger.LogDebug("Service information Swagger URL is relative, using base URL {BaseUrl}", baseUrl);
                
                if (baseUrl.EndsWith("/"))
                    baseUrl = baseUrl.Substring(0, baseUrl.Length - 1);
                
                serviceInfo.SwaggerUrl = baseUrl + serviceInfo.SwaggerUrl;
                
                logger.LogDebug("Service information Swagger URL is now {SwaggerUrl}", serviceInfo.SwaggerUrl);
            }

            return serviceInfo;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to retrieve service information from endpoint {Endpoint}: {Message}", serviceInfoEndpoint, ex.Message);
            return null;
        }
    }
}