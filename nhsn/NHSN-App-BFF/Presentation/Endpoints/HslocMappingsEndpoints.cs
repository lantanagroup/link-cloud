using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Hsloc;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The HSLOC Location Identification step's local-code-to-HSLOC rows, separate from /onboarding
// because they're backed by Normalization's Code Map operation, not the onboarding draft — mirrors
// EncounterMappingsEndpoints.
public class HslocMappingsEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/hsloc-mappings")
            .WithTags("HslocMappings")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/", async (IHslocMappingService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(cancellationToken)))
            .WithName("GetHslocMappings")
            .Produces<IReadOnlyList<HslocMapping>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "The facility's local Location-to-HSLOC code mappings.";
                operation.Description = "Empty until the facility has saved any mappings.";
                return operation;
            });

        group.MapPut("/", async (
                IReadOnlyList<HslocMapping> mappings,
                IHslocMappingService service,
                CancellationToken cancellationToken) =>
            {
                await service.SaveAsync(mappings, cancellationToken);
                return Results.NoContent();
            })
            .WithName("SaveHslocMappings")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Replace the facility's Location-to-HSLOC code mappings.";
                operation.Description = "Replaces the whole set for the facility — not an append.";
                return operation;
            });
    }
}
