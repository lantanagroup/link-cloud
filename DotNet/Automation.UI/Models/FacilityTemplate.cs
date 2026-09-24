namespace Automation.UI.Models;

/// <summary>
/// A reusable facility configuration. A scenario in facility mode uses one of these
/// and does not also pick query plan, normalization, organization resource map, or vendor.
/// </summary>
public sealed class FacilityTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Seeded template. View and clone only.</summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// The shared config system scenarios use. Only the system-default template is default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Tenant vendor name posted when the run creates the facility.
    /// Empty means the run does not send a vendor.
    /// </summary>
    public string? VendorName { get; set; }

    public Guid? QueryPlanTemplateId { get; set; }
    public Guid? NormalizationSuiteId { get; set; }
    public Guid? OrganizationResourceMapTemplateId { get; set; }

    /// <summary>
    /// When false, the run does not post an organization-location configuration,
    /// even if <see cref="OrganizationResourceMapTemplateId"/> is set.
    /// </summary>
    public bool EnableOrganizationLocationMapping { get; set; }

    public List<Guid> AllowedPatientConfigurationIds { get; set; } = [];

    /// <summary>
    /// When false, scenario cohorts may only use <see cref="AllowedPatientConfigurationIds"/>.
    /// </summary>
    public bool AllowPatientConfigurationsOutsideSet { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
