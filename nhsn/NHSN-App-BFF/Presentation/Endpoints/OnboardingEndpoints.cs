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
    }
}
