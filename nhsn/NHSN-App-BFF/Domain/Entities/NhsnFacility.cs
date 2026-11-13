using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Domain.Entities;

[Table("Facilities")]
public class NhsnFacility
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(64)]
    public string FacilityId { get; set; } = string.Empty;

    // Workflow position. The BFF owns this; the facility's configuration lives in Link.
    public OnboardingStatus OnboardingStatus { get; set; } = OnboardingStatus.NotStarted;

    // Cached from Tenant so /userinfo can answer without a Link round trip. A read cache, not a
    // second system of record — refreshed whenever the facility is read, and nothing authorises
    // against it.
    public EhrVendor? Vendor { get; set; }

    [MaxLength(64)]
    public string? CurrentStepId { get; set; }

    public DateTime? CompletedOn { get; set; }

    // Derived, never stored. PUT /facilities/{facilityId}/onboarding is part of the published
    // integration contract, so the route and its response keep their shape, but its handler sets
    // OnboardingStatus and nothing writes a standalone boolean.
    [NotMapped]
    public bool IsOnboarded => OnboardingStatus == OnboardingStatus.Complete;

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? CreatedBy { get; set; }

    public DateTime? LastModifiedOn { get; set; }

    [MaxLength(256)]
    public string? LastModifiedBy { get; set; }
}
