using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public interface IOnboardingWriteService
{
    // Saves the draft: workflow state always, plus the configuration for the step named by
    // currentStepId and no other section. Returns the re-read envelope, so the caller sees what the
    // owning services actually hold.
    Task<DraftEnvelopeResponse> SaveAsync(FacilityDraftResponse draft, CancellationToken cancellationToken = default);
}
