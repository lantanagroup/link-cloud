using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.OrganizationIdentification;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

public sealed class OrganizationIdentificationEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/organization-identification")
            .WithTags("Organization Identification")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/location-candidates", (string method, IOrganizationIdentificationService service) =>
                Results.Ok(service.GetLocationCandidates(method)))
            .WithName("GetLocationCandidates")
            .Produces<IReadOnlyList<LocationCandidateResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Candidate FHIR Locations for the Organization Identification step's search.";
                operation.Description = "Backs the Cerner \"Site\" location search. Results are simulated.";
                return operation;
            });
    }
}
