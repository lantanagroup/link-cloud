using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

// A facility's MRN Identifier Intake answers and rules. One row per facility.
//
// There is no Link microservice concept of "which identifier is the usable MRN" to own this data,
// so it lives here as a whole-object JSON blob — the same shape as OnboardingDraft.DraftJson. The
// UI sends and reads the whole MrnIntake object on every save, never a partial patch, so there is
// nothing to gain from a normalized schema.
[Table("MrnIntakeRecords")]
public class MrnIntakeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string FacilityId { get; set; } = string.Empty;

    // The MrnIntakeResponse object, serialized. See
    // Application/Models/Onboarding/MrnIntakeResponse.cs for the shape.
    public string IntakeJson { get; set; } = "{}";

    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? UpdatedBy { get; set; }
}
