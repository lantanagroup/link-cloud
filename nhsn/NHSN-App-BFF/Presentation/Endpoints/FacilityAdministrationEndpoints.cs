using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class FacilityAdministrationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/facilities")
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPut("/{facilityId}/onboarding", async (string facilityId, UpdateFacilityOnboardingRequest request, HttpContext httpContext, IFacilityAdministrationService facilityAdministrationService, IOptions<NhsnJwtSettings> jwtOptions, CancellationToken cancellationToken) =>
            {
                var actingFacilityId = httpContext.User.FindFirst(jwtOptions.Value.FacilityIdClaimType)?.Value;
                var groups = httpContext.User.FindAll(jwtOptions.Value.GroupsClaimType).Select(x => x.Value);
                var isFacilityAdmin = groups.Contains("FACADMIN", StringComparer.OrdinalIgnoreCase);

                if (string.IsNullOrWhiteSpace(actingFacilityId))
                {
                    return Results.BadRequest(new { message = "Facility context is required." });
                }

                try
                {
                    var updated = await facilityAdministrationService.UpdateFacilityOnboardingAsync(facilityId, actingFacilityId, isFacilityAdmin, request, cancellationToken);
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