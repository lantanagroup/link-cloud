using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reference;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// Reference data shared by every step and every facility.
public class ReferenceEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/reference")
            .WithTags("Reference")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/vendors", (IReferenceDataService referenceData) =>
                Results.Ok(referenceData.GetVendorProfiles()))
            .WithName("GetVendorProfiles")
            .Produces<IReadOnlyList<VendorProfile>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Vendor profiles driving all vendor-specific UI behaviour.";
                operation.Description =
                    "Everything that differs between Epic and Cerner, served as data so no step " +
                    "component contains a vendor name.";
                return operation;
            });

        group.MapGet("/timezones", (IReferenceDataService referenceData) =>
                Results.Ok(referenceData.GetTimezones()))
            .WithName("GetTimezones")
            .Produces<IReadOnlyList<TimezoneResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Selectable time zones, as IANA ids.";
                operation.Description = "Ids match what Tenant stores, so a selection round-trips unchanged.";
                return operation;
            });
    }
}
