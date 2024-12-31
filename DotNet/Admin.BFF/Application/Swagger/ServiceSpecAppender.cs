using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Swagger;

public class ServiceSpecAppender(
    IOptions<ServiceRegistry> serviceRegistry,
    HttpClient httpClient,
    ILogger<ServiceSpecAppender> logger) : IDocumentFilter
{
    private static readonly Dictionary<string, string> _responseCache = new();

    private async Task<OpenApiDocument> GetServiceSpec(string swaggerSpecUrl)
    {
        if (!_responseCache.TryGetValue(swaggerSpecUrl, out var response))
        {
            response = await httpClient.GetStringAsync(swaggerSpecUrl);
            _responseCache[swaggerSpecUrl] = response;
        }

        var openApiDocument = new OpenApiStringReader().Read(response, out var diagnostic);
        return openApiDocument;
    }

    private static string GetDotNetSwaggerSpecUrl(string serviceUrl)
    {
        return serviceUrl.TrimEnd('/') + "/swagger/v1/swagger.json";
    }

    private static string GetJavaSwaggerSpecUrl(string serviceUrl)
    {
        return serviceUrl.TrimEnd('/') + "/v3/api-docs";
    }

    private async Task AddServiceSpec(OpenApiDocument swaggerDoc, string swaggerSpecUrl)
    {
        try
        {
            var serviceSpec = await GetServiceSpec(swaggerSpecUrl);
            foreach (var path in serviceSpec.Paths)
            {
                // Assumes that the proxy path of the service is the same as the paths in the service spec
                swaggerDoc.Paths.Add(path.Key, path.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to add service spec to swagger doc");
        }
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var tasks = new List<Task>();

        if (!string.IsNullOrEmpty(serviceRegistry.Value.AuditServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.AuditServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.AccountServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.AccountServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.CensusServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.CensusServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.DataAcquisitionServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.DataAcquisitionServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.MeasureServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetJavaSwaggerSpecUrl(serviceRegistry.Value.MeasureServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.NormalizationServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.NormalizationServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.NotificationServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.NotificationServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.QueryDispatchServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.QueryDispatchServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.ReportServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.ReportServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.SubmissionServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.SubmissionServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.TenantService?.TenantServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetDotNetSwaggerSpecUrl(serviceRegistry.Value.TenantService.TenantServiceUrl)));

        if (!string.IsNullOrEmpty(serviceRegistry.Value.ValidationServiceUrl))
            tasks.Add(AddServiceSpec(swaggerDoc, GetJavaSwaggerSpecUrl(serviceRegistry.Value.ValidationServiceUrl)));

        Task.WhenAll(tasks).GetAwaiter().GetResult();
    }
}