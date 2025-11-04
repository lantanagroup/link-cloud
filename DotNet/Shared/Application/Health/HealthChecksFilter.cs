using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace LantanaGroup.Link.Shared.Application.Health;

public class HealthChecksFilter : IDocumentFilter
{
    private readonly IServiceProvider _serviceProvider;

    public HealthChecksFilter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Apply(OpenApiDocument openApiDocument, DocumentFilterContext context)
    {
        // Get the endpoint configuration from the application
        var endpointDataSource = _serviceProvider.GetRequiredService<EndpointDataSource>();
        var healthCheckEndpoint = endpointDataSource.Endpoints
            .FirstOrDefault(e => e.DisplayName?.Contains("Health checks") ?? false);

        if (healthCheckEndpoint == null)
        {
            return;
        }

        // Get the route pattern
        var routePattern = healthCheckEndpoint.Metadata
            .OfType<RoutePattern>()
            .FirstOrDefault()?.RawText ?? "/health";

        var pathItem = new OpenApiPathItem();
        var operation = new OpenApiOperation
        {
            Tags = new List<OpenApiTag> { new OpenApiTag { Name = "Health" } },
            Summary = "Health check endpoint",
            Description = "Returns the health status of the service and its dependencies"
        };

        // Get registered health checks
        var healthCheckService = _serviceProvider.GetRequiredService<HealthCheckService>();
        var registrations = _serviceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        // Create response schema
        var healthCheckResponseSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["status"] = new OpenApiSchema { Type = "string", Description = "Overall health status" },
                ["totalDuration"] = new OpenApiSchema { Type = "string", Description = "Total duration of health checks" },
                ["entries"] = new OpenApiSchema
                {
                    Type = "object",
                    Properties = registrations.ToDictionary(
                        r => r.Name,
                        _ => new OpenApiSchema
                        {
                            Type = "object",
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["status"] = new OpenApiSchema { Type = "string" },
                                ["description"] = new OpenApiSchema { Type = "string" },
                                ["duration"] = new OpenApiSchema { Type = "string" },
                                ["data"] = new OpenApiSchema 
                                { 
                                    Type = "object",
                                    AdditionalPropertiesAllowed = true 
                                }
                            }
                        }
                    )
                }
            }
        };

        // Add responses
        operation.Responses = new OpenApiResponses
        {
            ["200"] = new OpenApiResponse
            {
                Description = "Healthy",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = healthCheckResponseSchema
                    }
                }
            },
            ["503"] = new OpenApiResponse
            {
                Description = "Unhealthy",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = healthCheckResponseSchema
                    }
                }
            }
        };

        pathItem.AddOperation(OperationType.Get, operation);
        openApiDocument.Paths.Add(routePattern, pathItem);
    }
}