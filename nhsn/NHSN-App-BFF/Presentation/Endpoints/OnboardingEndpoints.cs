using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The onboarding draft: one read and one write, for every step.
public class OnboardingEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/onboarding")
            .WithTags("Onboarding")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/", async (IOnboardingReadService readService, CancellationToken cancellationToken) =>
            {
                var envelope = await readService.GetAsync(cancellationToken);
                return Results.Ok(envelope);
            })
            .WithName("GetOnboardingDraft")
            .Produces<DraftEnvelopeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Get the assembled onboarding draft for the authenticated facility.";
                operation.Description =
                    "Assembles FacilityDraft from Link, the facility row, the draft row and the BFF's tables. " +
                    "Returns 200 even when a downstream is unreachable: the response carries a per-section " +
                    "status in `sources`, and a step renders an error only when its own source failed. " +
                    "A section reported Unavailable is not the same as a section that read successfully " +
                    "and is empty.";
                return operation;
            });

        group.MapPut("/", async (
                FacilityDraftResponse draft,
                IOnboardingWriteService writeService,
                CancellationToken cancellationToken) =>
            {
                var envelope = await writeService.SaveAsync(draft, cancellationToken);
                return Results.Ok(envelope);
            })
            .WithName("SaveOnboardingDraft")
            .Produces<DraftEnvelopeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Save the onboarding draft for the authenticated facility.";
                operation.Description =
                    "Takes the whole FacilityDraft but writes only the section for currentStepId, " +
                    "which must be the step whose data the payload carries, sent before the " +
                    "transition is applied. Workflow state is always saved. 409 means another save " +
                    "for this facility is in flight; retry.";
                return operation;
            });

        group.MapPost("/import", async (
                IFormFile? file,
                IManualUploadTemplateService templateService,
                CancellationToken cancellationToken) =>
            {
                if (file is null || file.Length == 0)
                {
                    return Results.BadRequest(new {message = "No file was uploaded."});
                }

                await using var stream = file.OpenReadStream();
                var result = await templateService.ImportAsync(stream, cancellationToken);
                return Results.Ok(result);
            })
            .WithName("ImportOnboardingDraft")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImportResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Validate an uploaded manual-upload import sheet.";
                operation.Description =
                    "Parses the uploaded workbook and validates every recognized cell. Returns " +
                    "accepted=false with per-cell errors rather than a 4xx when the file parses " +
                    "but a value fails validation — that is a form-completion problem, not a " +
                    "request problem. A fully valid sheet is saved immediately, in this same call; " +
                    "any section that fails at save time (a cross-service precondition or a " +
                    "downstream validator the sheet's own format checks can't catch) is reported " +
                    "back as an additional per-cell error rather than failing the request.";
                return operation;
            });

        group.MapGet("/export", async (
                IPackageZipDownloadService packageService,
                CancellationToken cancellationToken) =>
            {
                var result = await packageService.ExportAsync(cancellationToken);
                return result.Status switch
                {
                    PackageZipDownloadStatus.Ok => Results.File(result.Content!, "application/zip", result.FileName),
                    PackageZipDownloadStatus.VendorNotSelected => Results.BadRequest(new
                    {
                        message = "Select an EHR vendor before downloading the import package."
                    }),
                    PackageZipDownloadStatus.AssetsUnavailable => Results.Problem(
                        title: "Import package assets unavailable",
                        statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
                };
            })
            .WithName("ExportOnboardingDraft")
            .Produces(StatusCodes.Status200OK, contentType: "application/zip")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Download the manual-upload import package for the facility's vendor.";
                operation.Description =
                    "A zip named {facilityId}_import_sheet.zip holding the vendor's import sheet, its " +
                    "census instructions and its JWKS instructions, plus the org-resolution guidance " +
                    "for Epic. Every entry is a byte-for-byte copy of the deployed static asset. " +
                    "400 when the facility has not chosen a vendor yet.";
                return operation;
            });

        group.MapPost("/completion", async (
                IOnboardingCompletionService completionService,
                CancellationToken cancellationToken) =>
            {
                var result = await completionService.CompleteAsync(cancellationToken);
                return Results.Ok(result);
            })
            .WithName("CompleteOnboarding")
            .Produces<CommitResultResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Finalizes the facility's enrollment.";
                operation.Description =
                    "The completion fan-out: arms the facility's Census acquisition job and confirms " +
                    "every other Link service onboarding touches holds the configuration this facility " +
                    "saved. Always returns 200 with a per-service status, even when a stage is " +
                    "pending or failed — the facility is marked onboarded only once every stage " +
                    "reports committed; a failed attempt leaves the prior configuration untouched " +
                    "and can be retried.";
                return operation;
            });

        group.MapGet("/completion", async (
                IOnboardingCompletionService completionService,
                CancellationToken cancellationToken) =>
                Results.Ok(await completionService.GetCommitStateAsync(cancellationToken)))
            .WithName("GetOnboardingCompletionState")
            .Produces<CommitResultResponse?>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "The facility's last completion attempt.";
                operation.Description = "Null before Complete Enrollment has been clicked.";
                return operation;
            });
    }
}
