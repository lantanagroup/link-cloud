using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;
using LantanaGroup.Link.Shared.Application.Models.Report;
using Link.Authorization.Infrastructure;
using Microsoft.OpenApi.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation;

public static class AggregationMapping
{
    public static RouteGroupBuilder MapAggregationEndpoints(this RouteGroupBuilder routes)
    {
        routes.WithOpenApi(x => new OpenApiOperation(x)
        {
            Tags = new List<OpenApiTag> { new() { Name = "Service Aggregation" } }
        });

        routes.MapGet("/reports/summaries", GetReportSummaries.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<List<ScheduledReportListSummary>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Get Report Summaries",
                Description = "Retrieves a list of report summaries."
            });

        return routes;
    }
}