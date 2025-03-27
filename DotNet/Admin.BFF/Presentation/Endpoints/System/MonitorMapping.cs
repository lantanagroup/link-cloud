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

        // routes.MapGet("", SearchQueryLogs.Handle)
        //     .ProducesProblem(StatusCodes.Status500InternalServerError)
        //     .WithOpenApi(x => new OpenApiOperation(x)
        //     {
        //         Summary = "Search Patient Acquisition Logs",
        //         Description = "Search all acquisition logs."
        //     });
        
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