using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The census step. Cerner's sFTP side and Epic's patient-list side are both fixture-backed —
// Cerner because LinkSdk has no sFTP coverage yet, Epic pending Q-21 — so every simulated
// response carries simulated: true.
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
                    "Cerner only. Tests the given connection details and returns success. Files " +
                    "and their patients are cached for GetSftpFiles, not returned here — there is " +
                    "no separate per-file preview call. Fixture-backed: every response carries " +
                    "simulated: true.";
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
                    "Epic only. Fixture-backed: every response carries simulated: true.";
                return operation;
            });

        group.MapPut("/acknowledgement", async (
                AcknowledgementRequest request,
                IPatientsOfInterestService service,
                CancellationToken cancellationToken) =>
            {
                await service.AcknowledgeCensusAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .WithName("AcknowledgeCensus")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Record the facility's census-accuracy acknowledgement.";
                operation.Description = "Append-only — every call adds a new attestation row rather than updating one.";
                return operation;
            });
    }
}
