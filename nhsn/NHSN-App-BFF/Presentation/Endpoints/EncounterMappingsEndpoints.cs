using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Encounter;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The Encounter Mapping step's local-code-to-CPT/SNOMED rows, separate from /onboarding because
// they're backed by Normalization's Code Map operation, not the onboarding draft
public class EncounterMappingsEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/encounter-mappings")
            .WithTags("EncounterMappings")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/", async (IEncounterMappingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(cancellationToken)))
            .WithName("GetEncounterMappings")
            .Produces<IReadOnlyList<EncounterMapping>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "The facility's local Encounter.type code mappings.";
                operation.Description = "Empty until the facility has saved any mappings.";
                return operation;
            });

        group.MapPut("/", async (
                IReadOnlyList<EncounterMapping> mappings,
                IEncounterMappingService service,
                CancellationToken cancellationToken) =>
            {
                await service.SaveAsync(mappings, cancellationToken);
                return Results.NoContent();
            })
            .WithName("SaveEncounterMappings")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Replace the facility's Encounter.type code mappings.";
                operation.Description = "Replaces the whole set for the facility — not an append.";
                return operation;
            });
    }
}
