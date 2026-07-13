using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class FacilityAdministrationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/facilities")
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet(string.Empty, async (IFacilityAdministrationService facilityAdministrationService, CancellationToken cancellationToken) =>
            {
                var facilities = await facilityAdministrationService.GetFacilitiesAsync(cancellationToken);
                return facilities.Count == 0 ? Results.NoContent() : Results.Ok(facilities);
            })
            .WithName("GetNhsnFacilities")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPut("/{facilityId}/onboarding", async (string facilityId, UpdateFacilityOnboardingRequest request, IFacilityAdministrationService facilityAdministrationService, CancellationToken cancellationToken) =>
            {
                var updated = await facilityAdministrationService.UpdateFacilityOnboardingAsync(facilityId, request, cancellationToken);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            })
            .WithName("UpdateFacilityOnboarding")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}