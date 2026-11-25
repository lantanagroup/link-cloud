using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The census step: Cerner's sFTP connection test and file preview, and Epic's patient lists, all
// served live by Data Acquisition.
public class PatientsOfInterestEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/patients-of-interest")
            .WithTags("PatientsOfInterest")
            .RequireAuthorization("AuthenticatedUser");

        group.MapPost("/sftp-connection-tests", async (
                SftpConfig config,
                IPatientsOfInterestService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.TestSftpConnectionAsync(config, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("TestSftpConnection")
            .Produces<ConnectionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Test a Cerner sFTP connection and cache the files it returns.")
            .WithDescription("Cerner only. Saves the given connection details, then tests them. When a " +
                "username and password are supplied, the files in the report directory and " +
                "their patients are cached for GetSftpFiles, not returned here; a test that " +
                "leaves them blank checks the saved credentials and lists no files.");

        group.MapGet("/sftp-files", async (IPatientsOfInterestService service, CancellationToken cancellationToken) =>
            {
                var files = await service.GetSftpFilesAsync(cancellationToken);
                return Results.Ok(files);
            })
            .WithName("GetSftpFiles")
            .Produces<IReadOnlyList<SftpFile>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("List the files from the last sFTP connection test.")
            .WithDescription("Cerner only. Served from cache, not a fresh call — empty until a connection " +
                "test has run for this facility.");

        group.MapPost("/list-queries", async (
                PatientListQueryRequest request,
                IPatientsOfInterestService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.QueryPatientListAsync(request.ListKey, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("QueryPatientList")
            .Produces<CensusListResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Run one of Epic's six patient-list census queries.")
            .WithDescription("Epic only. Reads the list's current patients from the facility's EHR through " +
                "Data Acquisition. An unknown or unconfigured list returns no patients.");

        group.MapGet("/list-queries", async (
                IPatientsOfInterestService service,
                CancellationToken cancellationToken) =>
            {
                var result = await service.QueryPatientListsAsync(cancellationToken);
                return Results.Ok(result);
            })
            .WithName("QueryPatientLists")
            .Produces<IReadOnlyList<CensusListResult>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status424FailedDependency)
            .WithSummary("Run all six of Epic's patient-list census queries in one call.")
            .WithDescription("Epic only. Data Acquisition resolves every configured list from the EHR in a " +
                "single round trip, so a FhirId it can't read fails the whole call (424) rather " +
                "than just its own list; the problem's listKey extension names which of the six " +
                "fields that id belongs to, when it can be determined.");

        group.MapPut("/sftp-credentials", async (
                SftpCredentialsRequest request,
                IPatientsOfInterestService service,
                CancellationToken cancellationToken) =>
            {
                await service.SaveSftpCredentialsAsync(request, cancellationToken);
                return Results.Accepted();
            })
            .WithName("SaveSftpCredentials")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Store Cerner sFTP credentials.")
            .WithDescription("Write-only — never echoed back by any read. Forwarded toward Data " +
                "Acquisition's credentials store; the BFF does not retain the values itself.");

        group.MapPut("/acknowledgement", async (
                AcknowledgementRequest request,
                IPatientsOfInterestService service,
                CancellationToken cancellationToken) =>
            {
                await service.AcknowledgeCensusAsync(request, cancellationToken);
                return Results.Accepted();
            })
            .WithName("AcknowledgeCensus")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Record the facility's census-accuracy acknowledgement.")
            .WithDescription("Append-only — every call adds a new attestation row rather than updating one.");
    }
}
