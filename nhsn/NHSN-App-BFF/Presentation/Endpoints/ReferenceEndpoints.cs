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
            .WithSummary("Vendor profiles driving all vendor-specific UI behaviour.")
            .WithDescription("Everything that differs between Epic and Cerner, served as data so no step " +
                "component contains a vendor name.");

        group.MapGet("/timezones", (IReferenceDataService referenceData) =>
                Results.Ok(referenceData.GetTimezones()))
            .WithName("GetTimezones")
            .Produces<IReadOnlyList<TimezoneResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Selectable time zones, as IANA ids.")
            .WithDescription("Ids match what Tenant stores, so a selection round-trips unchanged.");

        group.MapGet("/hsloc-codes", async (IReferenceDataService referenceData, CancellationToken cancellationToken) =>
                Results.Ok(await referenceData.GetHslocCodesAsync(cancellationToken)))
            .WithName("GetHslocCodes")
            .Produces<IReadOnlyList<HslocCode>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("The NHSN HSLOC reference vocabulary for the HSLOC Location Identification step.")
            .WithDescription("The whole catalog, every time — like /encounter-codes, the UI keeps the list " +
                "client-side and searches/filters it in place rather than querying per keystroke.");
    }
}
