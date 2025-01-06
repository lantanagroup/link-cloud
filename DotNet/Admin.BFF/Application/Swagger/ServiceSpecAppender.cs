using System.Collections.Concurrent;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using SharpYaml.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Swagger;

public class ServiceSpecAppender(
    IOptions<ServiceRegistry> serviceRegistry,
    HttpClient httpClient,
    ILogger<ServiceSpecAppender> logger,
    IOptions<ServiceSpecAppenderConfig> config) : IDocumentFilter
{
    private static readonly ConcurrentDictionary<string, string> _responseCache = new();

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

    private async Task<(OpenApiDocument? spec, string specUrl, string bffPrefix, string actualPrefix)> GetServiceSpec(string? serviceUrl, string? specUrl, string bffPrefix,
        string actualPrefix)
    {
        if (serviceUrl == null || specUrl == null) return (null, string.Empty, string.Empty, string.Empty);
        
        var fullSpecUrl = serviceUrl.TrimEnd('/') + "/" + specUrl.TrimStart('/');

        try
        {
            logger.LogInformation("Adding service spec {fullSpecUrl} to swagger doc", fullSpecUrl);
            var spec = await GetServiceSpec(fullSpecUrl);
            return (spec, fullSpecUrl, bffPrefix, actualPrefix);
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to add service spec {fullSpecUrl} to swagger doc: {ex.Message}");
            return (null, string.Empty, string.Empty, string.Empty);
        }
    }

    private void AddServiceSpec(OpenApiDocument doc, OpenApiDocument spec, string specUrl, string bffPrefix, string actualPrefix)
    {
        try
        {
            logger.LogInformation($"Adding service spec {specUrl} to swagger doc");

            foreach (var schema in spec.Components.Schemas)
            {
                var serviceSpecPrefix = spec.Info?.Title?.Replace(" ", string.Empty) ??
                                        specUrl.GetHashCode().ToString();
                var schemaName = schema.Key;
                var newSchemaName = schemaName;

                // Check for duplicates and rename if necessary
                if (doc.Components.Schemas.ContainsKey(schemaName))
                {
                    newSchemaName = $"{serviceSpecPrefix}{schemaName}";
                    RenameSchemaReferences(spec, schemaName, newSchemaName);
                }

                doc.Components.Schemas[newSchemaName] = schema.Value;
            }

            foreach (var path in spec.Paths)
            {
                PrefixOperationTags(path.Value, spec);
                
                string newPath = !string.IsNullOrEmpty(actualPrefix)
                    ? path.Key.Replace(actualPrefix, bffPrefix)
                    : path.Key;
                
                logger.LogDebug($"Adding path {newPath} (originally {path.Key} from {specUrl}");

                // Assumes that the proxy path of the service is the same as the paths in the service spec
                if (!doc.Paths.ContainsKey(newPath))
                    doc.Paths.Add(newPath, path.Value);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to add service spec {specUrl} to swagger doc: {ex.Message}");
        }
    }

    private static void PrefixOperationTags(OpenApiPathItem path, OpenApiDocument spec)
    {
        foreach (var operation in path.Operations)
        {
            if (operation.Value.Tags.Count == 0)
            {
                if (spec.Info?.Title != null)
                    operation.Value.Tags.Add(new OpenApiTag() { Name = spec.Info?.Title });
            }
            else
            {
                // Ensure the service spec's title is prefixed on each of the tags
                foreach (var tag in operation.Value.Tags)
                {
                    if (spec.Info?.Title != tag.Name)
                        tag.Name = $"{spec.Info?.Title} - {tag.Name}";
                }
            }
        }
    }

    private void RenameSchemaReferences(OpenApiDocument serviceSpec, string oldName, string newName)
    {
        var processedSchemas = new HashSet<OpenApiSchema>();
        
        foreach (var path in serviceSpec.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                RenameSchemaInParameters(operation.Parameters, oldName, newName);
                RenameSchemaInRequestBody(operation.RequestBody, oldName, newName);
                RenameSchemaInResponses(operation.Responses, oldName, newName);
            }
        }

        foreach (var component in serviceSpec.Components.Schemas.Values)
        {
            RenameSchemaInSchema(component, oldName, newName, processedSchemas);
        }
    }

    private void RenameSchemaInParameters(IList<OpenApiParameter> parameters, string oldName, string newName)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.Schema.Reference != null && parameter.Schema.Reference.Id == oldName)
            {
                parameter.Schema.Reference.Id = newName;
            }
        }
    }

    private void RenameSchemaInRequestBody(OpenApiRequestBody requestBody, string oldName, string newName)
    {
        if (requestBody == null) return;

        foreach (var content in requestBody.Content.Values)
        {
            if (content?.Schema?.Reference != null && content?.Schema?.Reference?.Id == oldName)
            {
                content.Schema.Reference.Id = newName;
            }
        }
    }

    private void RenameSchemaInResponses(OpenApiResponses responses, string oldName, string newName)
    {
        foreach (var response in responses.Values)
        {
            foreach (var content in response.Content.Values)
            {
                if (content.Schema.Reference != null && content.Schema.Reference.Id == oldName)
                {
                    content.Schema.Reference.Id = newName;
                }
            }
        }
    }

    private void RenameSchemaInSchema(OpenApiSchema schema, string oldName, string newName, HashSet<OpenApiSchema> processedSchemas)
    {
        if (processedSchemas.Contains(schema))
        {
            return;
        }

        processedSchemas.Add(schema);

        if (schema.Reference != null && schema.Reference.Id == oldName)
        {
            schema.Reference.Id = newName;
        }

        foreach (var property in schema.Properties.Values)
        {
            RenameSchemaInSchema(property, oldName, newName, processedSchemas);
        }
    }
    
    public static string GetFullSpecUrl(string serviceUrl, string specUrl)
    {
        return serviceUrl.TrimEnd('/') + specUrl;
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Update tags for already-existing operations to be prefixed with "BFF - "
        foreach (var path in swaggerDoc.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                foreach (var tag in operation.Value.Tags)
                {
                    if (!string.IsNullOrEmpty(tag.Name) && !tag.Name.StartsWith("Admin.BFF"))
                        tag.Name = $"Admin.BFF - {tag.Name}";
                    else if (string.IsNullOrEmpty(tag.Name))
                        tag.Name = "Admin.BFF - General";
                }
            }
        }
        
        var tasks = new List<Task<(OpenApiDocument? spec, string specUrl, string bffPrefix, string actualPrefix)>>
        {
            GetServiceSpec(serviceRegistry.Value.AccountServiceUrl, config.Value.AccountServiceSpecUrl, config.Value.AccountServiceBffPrefix, config.Value.AccountServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.AuditServiceUrl, config.Value.AuditServiceSpecUrl, config.Value.AuditServiceBffPrefix, config.Value.AuditServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.CensusServiceUrl, config.Value.CensusServiceSpecUrl, config.Value.CensusServiceBffPrefix, config.Value.CensusServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.DataAcquisitionServiceUrl, config.Value.DataAcquisitionServiceSpecUrl, config.Value.DataAcquisitionServiceBffPrefix, config.Value.DataAcquisitionServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.MeasureServiceUrl, config.Value.MeasureServiceSpecUrl, config.Value.MeasureServiceBffPrefix, config.Value.MeasureServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.NormalizationServiceUrl, config.Value.NormalizationServiceSpecUrl, config.Value.NormalizationServiceBffPrefix, config.Value.NormalizationServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.NotificationServiceUrl, config.Value.NotificationServiceSpecUrl, config.Value.NotificationServiceBffPrefix, config.Value.NotificationServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.QueryDispatchServiceUrl, config.Value.QueryDispatchServiceSpecUrl, config.Value.QueryDispatchServiceBffPrefix, config.Value.QueryDispatchServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.ReportServiceUrl, config.Value.ReportServiceSpecUrl, config.Value.ReportServiceBffPrefix, config.Value.ReportServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.SubmissionServiceUrl, config.Value.SubmissionServiceSpecUrl, config.Value.SubmissionServiceBffPrefix, config.Value.SubmissionServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.TenantService.TenantServiceUrl, config.Value.TenantServiceSpecUrl, config.Value.TenantServiceBffPrefix, config.Value.TenantServiceActualPrefix),
            GetServiceSpec(serviceRegistry.Value.ValidationServiceUrl, config.Value.ValidationServiceSpecUrl, config.Value.ValidationServiceBffPrefix, config.Value.ValidationServiceActualPrefix)
        };

        var results = Task.WhenAll(tasks).GetAwaiter().GetResult();

        foreach ((OpenApiDocument spec, string specUrl, string bffPrefix, string actualPrefix) in results)
        {
            if (spec == null) continue;
            AddServiceSpec(swaggerDoc, spec, specUrl, bffPrefix, actualPrefix);
        }
    }
}