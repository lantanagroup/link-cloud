using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
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
            Tags = new HashSet<OpenApiTagReference> { new OpenApiTagReference("Health", openApiDocument) },
            Summary = "Health check endpoint",
            Description = "Returns the health status of the service and its dependencies"
        };

        // Get registered health checks
        var healthCheckService = _serviceProvider.GetRequiredService<HealthCheckService>();
        var registrations = _serviceProvider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        // Create response schema
        var healthCheckResponseSchema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Overall health status" },
                ["totalDuration"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Total duration of health checks" },
                ["entries"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = registrations.ToDictionary(
                        r => r.Name,
                        _ => (IOpenApiSchema)new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["status"] = new OpenApiSchema { Type = JsonSchemaType.String },
                                ["description"] = new OpenApiSchema { Type = JsonSchemaType.String },
                                ["duration"] = new OpenApiSchema { Type = JsonSchemaType.String },
                                ["data"] = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.Object,
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

        pathItem.AddOperation(HttpMethod.Get, operation);
        openApiDocument.Paths.Add(routePattern, pathItem);
    }
}