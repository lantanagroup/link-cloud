using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Facility;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Facility;
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

        routes.MapDelete("/facility/{facilityId}", SoftDeleteFacility.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<FacilitySoftDeleteResult>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Soft Delete Facility",
                Description = "Soft deletes a facility and its associated report schedules, acquisition logs, and census cron jobs. " +
                              "Returns 409 if reports are currently running. " +
                              "Returns 404 if the facility does not exist. " +
                              "If a downstream step fails, all completed steps are rolled back and 500 is returned."
            });

        routes.MapPatch("/facility/{facilityId}/restore", RestoreFacility.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<FacilityRestoreResult>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Restore Facility",
                Description = "Restores a soft-deleted facility and its associated report schedules, acquisition logs, and census cron jobs. " +
                              "Returns 404 if the facility does not exist. " +
                              "If a downstream step fails, all completed steps are rolled back and 500 is returned."
            });

        routes.MapDelete("/reports/{reportScheduleId}", SoftDeleteReport.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Soft Delete Report Schedule",
                Description = "Soft deletes a report schedule and its associated acquisition logs. " +
                              "Returns 409 if the report is currently in progress (New or EndOfPeriod status). " +
                              "Returns 404 if the report schedule does not exist. " +
                              "If the acquisition log deletion fails, the report soft-delete is rolled back and 500 is returned."
            });

        routes.MapPost("/reports/{reportScheduleId}/abort", AbortReport.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Abort In-Progress Report",
                Description = "Stops an in-progress report (New or EndOfPeriod) without affecting other reports for the facility. " +
                              "Queued Data Acquisition and Normalization work for this report is dropped; census jobs are left running. " +
                              "The report schedule is then soft-deleted so it can be restored later. " +
                              "Returns 409 if the report is not in progress."
            });

        routes.MapPatch("/reports/{reportScheduleId}/restore", RestoreReport.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Restore Report Schedule",
                Description = "Restores a soft-deleted report schedule and its associated acquisition logs. " +
                              "Returns 404 if the report schedule does not exist. " +
                              "If the acquisition log restore fails, the report restore is rolled back and 500 is returned."
            });

        routes.MapGet("/reports/summaries", GetReportSummaries.Search)
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

        routes.MapGet("/reports/summaries/{reportScheduleId}", GetReportSummaries.Get)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<ScheduledReportListSummary>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Summary = "Get Report Summary by ID",
                Description = "Retrieves a single report summary by report schedule ID."
            });

        return routes;
    }
}