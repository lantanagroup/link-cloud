using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// Cerner's sFTP file listing. Fixture-backed until LinkSdk gains sFTP coverage — the shape is
// fully specified (Data Acquisition's ad-hoc test-connection call, includeFileContent=true), so
// every response carries simulated: true rather than being gated behind a capability flag.
public class PatientsOfInterestEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/patients-of-interest")
            .WithTags("PatientsOfInterest")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/sftp-files", async (IPatientsOfInterestService service, CancellationToken cancellationToken) =>
            {
                var files = await service.GetSftpFilesAsync(cancellationToken);
                return Results.Ok(files);
            })
            .WithName("GetSftpFiles")
            .Produces<IReadOnlyList<SftpFile>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "List sFTP files queued for the authenticated facility, patients attached.";
                operation.Description =
                    "Cerner only. Fixture-backed: LinkSdk has no sFTP coverage yet, so every " +
                    "response carries simulated: true. One call — files carry their patients " +
                    "already, there is no separate per-file preview.";
                return operation;
            });
    }
}
