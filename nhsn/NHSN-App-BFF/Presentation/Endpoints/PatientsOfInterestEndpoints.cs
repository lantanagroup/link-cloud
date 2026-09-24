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
            .WithOpenApi(operation =>
            {
                operation.Summary = "Test a Cerner sFTP connection and cache the files it returns.";
                operation.Description =
                    "Cerner only. Saves the given connection details, then tests them. When a " +
                    "username and password are supplied, the files in the report directory and " +
                    "their patients are cached for GetSftpFiles, not returned here; a test that " +
                    "leaves them blank checks the saved credentials and lists no files.";
                return operation;
            });

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
                operation.Summary = "List the files from the last sFTP connection test.";
                operation.Description =
                    "Cerner only. Served from cache, not a fresh call — empty until a connection " +
                    "test has run for this facility.";
                return operation;
            });

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
            .WithOpenApi(operation =>
            {
                operation.Summary = "Run one of Epic's six patient-list census queries.";
                operation.Description =
                    "Epic only. Reads the list's current patients from the facility's EHR through " +
                    "Data Acquisition. An unknown or unconfigured list returns no patients.";
                return operation;
            });

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
            .WithOpenApi(operation =>
            {
                operation.Summary = "Run all six of Epic's patient-list census queries in one call.";
                operation.Description =
                    "Epic only. Data Acquisition resolves every configured list from the EHR in a " +
                    "single round trip, so a FhirId it can't read fails the whole call (424) rather " +
                    "than just its own list; the problem's listKey extension names which of the six " +
                    "fields that id belongs to, when it can be determined.";
                return operation;
            });

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
            .WithOpenApi(operation =>
            {
                operation.Summary = "Store Cerner sFTP credentials.";
                operation.Description =
                    "Write-only — never echoed back by any read. Forwarded toward Data " +
                    "Acquisition's credentials store; the BFF does not retain the values itself.";
                return operation;
            });

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
            .WithOpenApi(operation =>
            {
                operation.Summary = "Record the facility's census-accuracy acknowledgement.";
                operation.Description = "Append-only — every call adds a new attestation row rather than updating one.";
                return operation;
            });
    }
}
