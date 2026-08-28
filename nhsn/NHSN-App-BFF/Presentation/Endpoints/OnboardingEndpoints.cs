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
                    "request problem. Writes nothing; the caller saves the draft separately once " +
                    "the file is accepted.";
                return operation;
            });

        group.MapGet("/export", async (
                IManualUploadTemplateService templateService,
                CancellationToken cancellationToken) =>
            {
                var content = await templateService.ExportAsync(cancellationToken);
                return Results.File(
                    content,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "manual-upload-import-sheet.xlsx");
            })
            .WithName("ExportOnboardingDraft")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithOpenApi(operation =>
            {
                operation.Summary = "Download the manual-upload import sheet, pre-filled from the current draft.";
                return operation;
            });
    }
}
