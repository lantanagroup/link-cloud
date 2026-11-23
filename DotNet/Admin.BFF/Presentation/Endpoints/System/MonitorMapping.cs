using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Health;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Handlers;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System;

public static class MonitorMapping
{
    public static RouteGroupBuilder MapMonitorEndpoints(this RouteGroupBuilder routes)
    {
        routes.WithTags("System Information");

        routes.MapGet("/health", GetSystemHealth.Handle)
            .Produces<IEnumerable<LinkServiceHealthReport>>(StatusCodes.Status200OK)
            .WithSummary("System Health Check")
            .WithDescription("Checks the health status of the system.");

        routes.MapGet("/health/{service}", GetServiceHealth.Handle)
            .Produces<LinkServiceHealthReport>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Service Health Check")
            .WithDescription("Checks the health status of the specified service.");

        return routes;
    }
}