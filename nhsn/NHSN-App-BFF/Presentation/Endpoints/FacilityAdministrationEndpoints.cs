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
                    return Results.Problem(
                        title: "Facility context is required",
                        detail: "Facility context is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                try
                {
                    var updated = await facilityAdministrationService.UpdateFacilityOnboardingAsync(facilityId, request, cancellationToken);
                    return updated is null ? Results.NotFound() : Results.Accepted(value: updated);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(
                        title: "The facility onboarding update was rejected",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status400BadRequest);
                }
            })
            .WithName("UpdateFacilityOnboarding")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
