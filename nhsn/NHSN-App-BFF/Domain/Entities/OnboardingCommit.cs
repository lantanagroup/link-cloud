using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

// The result of the most recent completion attempt for a facility. One row per facility, replaced
// on every attempt — a retry after CommitFailed overwrites the prior detail rather than appending
// to a history, matching NhsnFacility.OnboardingStatus, which the completion fan-out also owns and
// which likewise carries only the current state.
[Table("OnboardingCommits")]
public class OnboardingCommit
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string FacilityId { get; set; } = string.Empty;

    // The CommitResultResponse object, serialized. See
    // Application/Models/Onboarding/CommitResultResponse.cs for the shape.
    public string ResultJson { get; set; } = "{}";

    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
}
