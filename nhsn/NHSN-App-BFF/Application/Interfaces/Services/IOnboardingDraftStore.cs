using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

// Reads and writes the OnboardingDrafts row — the BFF-owned half of the onboarding picture.
//
// Deliberately narrow: it knows about workflow state and nothing about Link. Assembling the full
// draft from this plus the Link gateways plus the BFF's normalized tables is the job of the service
// above it, which keeps this type unaware of whether a downstream was reachable.
public interface IOnboardingDraftStore
{
    // Returns the stored workflow state, or an empty one when the facility has no draft yet. Absent
    // is not an error — a facility whose token has been seen but whose first step hasn't been saved
    // legitimately has no row.
    Task<StoredDraft> GetAsync(string facilityId, CancellationToken cancellationToken = default);

    Task SaveAsync(string facilityId, StoredDraft draft, CancellationToken cancellationToken = default);
}

// The OnboardingDrafts row, in our vocabulary rather than EF's.
public sealed record StoredDraft
{
    public OnboardingDraftState State { get; init; } = new();

    // Steps the user has reached. Stored in its own column rather than inside DraftJson — it's a set
    // the BFF may need to reason about, not opaque UI state. Reaching a step isn't the same as being
    // allowed on it: gating also requires every prerequisite to be complete.
    public IReadOnlyList<string> UnlockedStepIds { get; init; } = [];

    // The version the row was stored at, before migrate-on-read ran.
    public int SchemaVersion { get; init; }
}
