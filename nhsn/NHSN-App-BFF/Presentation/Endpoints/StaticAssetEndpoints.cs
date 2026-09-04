using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public sealed class StaticAssetEndpoints : IApi
{
    private static readonly Dictionary<string, string> JwksInstructionsPdfByVendor =
        VendorProfileCatalog.All.ToDictionary(
            profile => profile.DisplayName,
            profile => $"{profile.DisplayName}_JWKS_Instructions.pdf",
            StringComparer.OrdinalIgnoreCase);

    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/static")
            .WithTags("NHSN App BFF - Static Assets")
            .AllowAnonymous();

        group.MapGet("/jwks-instructions/{vendor}", (string vendor, IWebHostEnvironment environment) =>
            {
                if (!JwksInstructionsPdfByVendor.TryGetValue(vendor, out var fileName))
                {
                    return Results.NotFound();
                }

                var filePath = Path.Combine(environment.ContentRootPath, "StaticAssets", "jwks-instructions", fileName);

                return File.Exists(filePath)
                    ? Results.File(filePath, "application/pdf")
                    : Results.NotFound();
            })
            .WithName("GetJwksInstructionsPdf")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // One PDF, not per-vendor.
        group.MapGet("/location-org-resolution", (IWebHostEnvironment environment) =>
            {
                var filePath = Path.Combine(environment.ContentRootPath, "StaticAssets", "location-org-resolution", "Location_Org_Resolution.pdf");

                return File.Exists(filePath)
                    ? Results.File(filePath, "application/pdf")
                    : Results.NotFound();
            })
            .WithName("GetLocationOrgResolutionPdf")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}
