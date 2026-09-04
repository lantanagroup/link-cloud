namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

// Where a facility is in the onboarding workflow.
//
// The single system of record for whether a facility is onboarded. NhsnFacility.IsOnboarded is
// derived from this and never written independently.
public enum OnboardingStatus
{
    // The facility row exists because a token named it, but no step has been saved.
    NotStarted,

    InProgress,

    // The arming writes are in flight.
    Committing,

    Complete,

    // Arming failed partway. Inert configuration survives; the retry targets what is outstanding.
    CommitFailed
}
