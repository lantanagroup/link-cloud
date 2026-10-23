using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

// The workflow state behind one facility's onboarding session. One row per facility.
//
// Workflow state only — no configuration value is persisted here. Configuration belongs to the
// Link service that owns it, from the moment the step capturing it completes. What lives here is
// step position, sub-view, the unlock set, per-step UI flags, and contract-pending sections that
// have no Link owner yet. Typically 1-2 KB.
//
// Not to be confused with FacilityDraft, the shape GET /onboarding returns — that one is assembled
// per request from three sources (Link, this row, BFF tables) and is never stored.
//
// Carries no RowVersion and PUT /onboarding takes no If-Match: a lost race here costs a step
// position rather than a step's work.
[Table("OnboardingDrafts")]
public class OnboardingDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string FacilityId { get; set; } = string.Empty;

    // Schema version of DraftJson, so an older draft can be migrated on read rather than failing
    // to deserialize.
    public int SchemaVersion { get; set; }

    // Workflow state as JSON. See the remarks on this type for what may not go in here.
    public string DraftJson { get; set; } = "{}";

    // Step ids the user has unlocked, as a JSON array.
    public string UnlockedStepsJson { get; set; } = "[]";

    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
