using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class FacilityAdministrationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/facilities")
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPut("/{facilityId}/onboarding", async (string facilityId, UpdateFacilityOnboardingRequest request, INhsnUserContext userContext, IFacilityAdministrationService facilityAdministrationService, CancellationToken cancellationToken) =>
            {
                if (!userContext.HasFacility)
                {
                    return Results.BadRequest(new { message = "Facility context is required." });
                }

                try
                {
                    var updated = await facilityAdministrationService.UpdateFacilityOnboardingAsync(facilityId, request, cancellationToken);
                    return updated is null ? Results.NotFound() : Results.Ok(updated);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .WithName("UpdateFacilityOnboarding")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
