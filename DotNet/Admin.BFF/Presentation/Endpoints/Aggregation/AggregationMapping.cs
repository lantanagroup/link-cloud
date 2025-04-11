using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Tenant;
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

        routes.MapGet("/facility/adhocReportRequest", PostAdhocReportRequested.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<HttpResponseMessage>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Generate an AdhocReport For Facility",
                Description = "Initiates the process of generating an AdHoc Report for a given Facility."
            });

        routes.MapGet("/facility/regenerateReportRequest", PostGenerateReportRequest.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<HttpResponseMessage>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Regenerate a previously scheduled Report For Facility",
                Description = "Initiates the process of regenerating a previously scheduled Report for a given Facility."
            });
        return routes;
    }
}