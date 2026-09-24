using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class EncounterEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/encounter")
            .WithTags("Encounter")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/encounter-codes", async (IReferenceDataService referenceData, CancellationToken cancellationToken) =>
                Results.Ok(await referenceData.GetEncounterCodesAsync(cancellationToken)))
            .WithName("GetEncounterCodes")
            .Produces<IReadOnlyList<EncounterCode>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "CPT/SNOMED reference codes for the Encounter Mapping step, expanded live from Terminology.";
                operation.Description =
                    "Returns the whole expansion for every configured ValueSet; the UI filters it " +
                    "client-side. Terminology's ValueSet/$expand has no filter parameter of its own.";
                return operation;
            });

        group.MapGet("/encounter-codes/lookup", async (string system, string code, IReferenceDataService referenceData, CancellationToken cancellationToken) =>
                Results.Ok(await referenceData.LookupEncounterCodeAsync(system.Sanitize(), code.Sanitize(), cancellationToken)))
            .WithName("LookupEncounterCode")
            .Produces<EncounterCodeDetail?>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "The source CodeSystem's name/version for one encounter code.";
                operation.Description =
                    "CodeSystem/$lookup, for the reference tab's detail panel. Returns a null body " +
                    "when Terminology has no CodeSystem loaded for `system`, or `code` is not one " +
                    "of its members — not an error condition.";
                return operation;
            });
    }
}
