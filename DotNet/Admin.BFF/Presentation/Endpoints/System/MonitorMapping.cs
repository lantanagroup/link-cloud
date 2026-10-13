using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Handlers;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System;

public static class MonitorMapping
{
    public static RouteGroupBuilder MapMonitorEndpoints(this RouteGroupBuilder routes)
    {
        routes.WithOpenApi(x => new OpenApiOperation(x)
        {
            Tags = new List<OpenApiTag> { new() { Name = "System Information" } }
        });

        routes.MapGet("/health", GetSystemHealth.Handle)
            .Produces<IEnumerable<LinkServiceHealthReport>>(StatusCodes.Status200OK)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "System Health Check",
                Description = "Checks the health status of the system."
            });

        routes.MapGet("/health/{service}", GetServiceHealth.Handle)
            .Produces<LinkServiceHealthReport>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Service Health Check",
                Description = "Checks the health status of the specified service."
            });

        return routes;
    }
}