using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public class FhirServerEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/fhir-server")
            .WithTags("NHSN App BFF")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPost("/connection-tests", async (FhirConnectionTestRequest request, IFacilityAdministrationService facilityAdministrationService, CancellationToken cancellationToken) =>
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
