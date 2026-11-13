using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// The completion fan-out: NHSN-App-BFF's own arming writes and readiness checks across every Link
// service onboarding touches, run once when the facility clicks "Complete Enrollment" on the MRN
// Identifier Intake step. Owns the only writes to NhsnFacility.OnboardingStatus's
// Committing/Complete/CommitFailed states — see the remarks on that enum and on
// OnboardingWriteService.SaveWorkflowStateAsync, which explicitly must not walk them backwards.
public interface IOnboardingCompletionService
{
    Task<CommitResultResponse> CompleteAsync(CancellationToken cancellationToken = default);

    // The last completion attempt's result, or null before one has been made.
    Task<CommitResultResponse?> GetCommitStateAsync(CancellationToken cancellationToken = default);
}
