using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The measures a facility can actually report on. Shared by the Reporting Plan step (its
// completion gate) and the Generate Test Report step (its measure picker) -- each fetches its
// own copy fresh on mount rather than reading a shared cache, so neither goes stale relative to
// the other.
public class ReportingPlanEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/reporting-plan")
            .WithTags("ReportingPlan")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/measures", async (
                IReportingPlanGateway gateway,
                INhsnUserContext userContext,
                CancellationToken cancellationToken) =>
                Results.Ok(await gateway.GetAvailableMeasuresAsync(userContext.RequireFacilityId(), cancellationToken)))
            .WithName("GetAvailableMeasures")
            .Produces<IReadOnlyList<AvailableMeasure>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "The current facility's reporting-plan measures that MeasureEval can evaluate.";
                operation.Description =
                    "Cross-references the facility's DMRP reporting plan against MeasureEval's " +
                    "loaded measure definitions. A facility enrolled only in measures MeasureEval " +
                    "has no definition for gets an empty list -- correctly, not as an error.";
                return operation;
            });
    }
}
