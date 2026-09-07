using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class EncounterEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/encounter")
            .WithTags("Encounter")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/encounter-codes", (string? q, IReferenceDataService referenceData) =>
                Results.Ok(referenceData.GetEncounterCodes(q)))
            .WithName("GetEncounterCodes")
            .Produces<IReadOnlyList<EncounterCode>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "CPT/SNOMED reference codes for the Encounter Mapping step.";
                operation.Description =
                    "Optional q: case-insensitive match across system, code, display, category " +
                    "and categoryName. Omit it (or pass it blank) for the whole catalog, which is " +
                    "what the UI does — it keeps the list and filters it in place.";
                return operation;
            });
    }
}
