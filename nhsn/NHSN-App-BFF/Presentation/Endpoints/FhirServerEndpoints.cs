using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.FacilityAdministration;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class FhirServerEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/fhir-server")
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/", async (INhsnUserContext userContext, IFacilityAdministrationService facilityAdministrationService, CancellationToken cancellationToken) =>
            {
                if (!userContext.HasFacility)
                {
                    return Results.BadRequest(new { message = "Facility context is required." });
                }

                try
                {
                    var info = await facilityAdministrationService.GetFhirServerInfoAsync(cancellationToken);
                    return info is null ? Results.NotFound() : Results.Ok(info);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .WithName("GetFhirServerInfo")
            .Produces<FhirServerInfoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/test-connection", async (FhirConnectionTestRequest request, IFacilityAdministrationService facilityAdministrationService, CancellationToken cancellationToken) =>
            {
                var result = await facilityAdministrationService.TestFhirConnectionAsync(request.FhirServerBaseUrl, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("TestFhirConnection")
            .Produces(StatusCodes.Status200OK)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Probe a FHIR server base URL for reachability.";
                operation.Description =
                    "URL-only reachability probe against Data Acquisition's connectionValidation " +
                    "endpoint. Proves the server responds, not that Link's own credentials can pull " +
                    "data from it — the facility-scoped probe after commit is the honest end-to-end " +
                    "check.";
                return operation;
            });
    }
}

public class FhirConnectionTestRequest
{
    public string FhirServerBaseUrl { get; set; } = string.Empty;
}
