using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Hanlders;
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
            .Produces(StatusCodes.Status200OK)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "System Health Check",
                Description = "Checks the health status of the system."
            });
        
        return routes;
    }
}