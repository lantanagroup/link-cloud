using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Facility;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Validation;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Facility;
using LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation.Handlers.Report;
using LantanaGroup.Link.Shared.Application.Models.Report;
using Link.Authorization.Infrastructure;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.Aggregation;

public static class AggregationMapping
{
    public static RouteGroupBuilder MapAggregationEndpoints(this RouteGroupBuilder routes)
    {
        routes.WithTags("Service Aggregation");

        routes.MapDelete("/facility/{facilityId}", SoftDeleteFacility.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<FacilitySoftDeleteResult>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Soft Delete Facility")
            .WithDescription("Soft deletes a facility and its associated report schedules, acquisition logs, and census cron jobs. " +
                "Returns 409 if reports are currently running. " +
                "Returns 404 if the facility does not exist. " +
                "If a downstream step fails, all completed steps are rolled back and 500 is returned.");

        routes.MapPatch("/facility/{facilityId}/restore", RestoreFacility.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<FacilityRestoreResult>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Restore Facility")
            .WithDescription("Restores a soft-deleted facility and its associated report schedules, acquisition logs, and census cron jobs. " +
                "Returns 404 if the facility does not exist. " +
                "If a downstream step fails, all completed steps are rolled back and 500 is returned.");

        routes.MapDelete("/reports/{reportScheduleId}", SoftDeleteReport.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Soft Delete Report Schedule")
            .WithDescription("Soft deletes a report schedule and its associated acquisition logs. " +
                "Returns 409 if the report is currently in progress (New or EndOfPeriod status). " +
                "Returns 404 if the report schedule does not exist. " +
                "If the acquisition log deletion fails, the report soft-delete is rolled back and 500 is returned.");

        routes.MapPost("/reports/{reportScheduleId}/abort", AbortReport.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Abort In-Progress Report")
            .WithDescription("Stops an in-progress report (New or EndOfPeriod) without affecting other reports for the facility. " +
                "Queued Data Acquisition and Normalization work for this report is dropped; census jobs are left running. " +
                "The report schedule is then soft-deleted. Reports deleted mid flight cannot be restored. " +
                "Returns 409 if the report is not in progress.");

        routes.MapPatch("/reports/{reportScheduleId}/restore", RestoreReport.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Restore Report Schedule")
            .WithDescription("Restores a soft-deleted report schedule and its associated acquisition logs " +
                "when the schedule had not started (Scheduled) or had finished submission (Submitted). " +
                "Returns 409 when the report was deleted in any other status. " +
                "Returns 404 if the report schedule does not exist. " +
                "If the acquisition log restore fails, the report restore is rolled back and 500 is returned.");

        routes.MapGet("/reports/summaries", GetReportSummaries.Search)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<List<ScheduledReportListSummary>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get Report Summaries")
            .WithDescription("Retrieves a list of report summaries.");

        routes.MapGet("/reports/summaries/{reportScheduleId}", GetReportSummaries.Get)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<ScheduledReportListSummary>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithSummary("Get Report Summary by ID")
            .WithDescription("Retrieves a single report summary by report schedule ID.");

        routes.MapGet("/reports/{reportScheduleId}/validation-category-counts",
                      GetReportValidationCategoryCounts.Handle)
            .RequireAuthorization(LinkAuthorizationConstants.LinkBearerService.AuthenticatedUserPolicyName)
            .Produces<ReportValidationCategoryCounts>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithSummary("Get Report Validation Category Counts")
            .WithDescription("Retrieves a report's stored validation counts per category, most issues first, " +
                "with each category's current guidance and severity. " +
                "Counts are written as each patient is validated and are kept after the report's " +
                "findings are deleted. Pass submit=true or submit=false to return only categories " +
                "with that submit flag. " +
                "inProgress is true while the report is still running, so the counts cover only " +
                "the patients validated so far; reportStatus is the schedule's status in Report. " +
                "Returns 404 if the report schedule does not exist, or if it has no stored counts " +
                "(for example, it was validated before counts were stored); " +
                "the problem detail says which. " +
                "Returns 502 if Report or Validation returns a response that breaks its contract.");

        return routes;
    }
}