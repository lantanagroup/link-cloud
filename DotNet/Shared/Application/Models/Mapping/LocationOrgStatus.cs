namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// Whether a patient's encounters resolved to the reporting organization during acquisition.
/// </summary>
/// <remarks>
/// Deliberately coarse. The finer distinction between a patient verified against the facility's
/// org-location configuration and one accepted by the permissive no-location-references default is
/// derived by the consumer from the counts on <see cref="LocationOrgOutcome"/>, so recognizing it does
/// not change this contract.
/// </remarks>
public enum LocationOrgStatus
{
    /// <summary>
    /// The question does not apply: org-location mapping is not active for the facility, or the patient
    /// had no encounters to resolve. Distinct from <see cref="NotFound"/>, which is a real negative.
    /// </summary>
    NotApplicable,

    /// <summary>
    /// At least one of the patient's encounters mapped to the reporting organization, so the patient is
    /// in scope for the report.
    /// </summary>
    Found,

    /// <summary>
    /// Org-location mapping was active and the patient had encounters, but none mapped to the reporting
    /// organization. The patient's encounters are stripped from the acquisition cache in this case.
    /// </summary>
    NotFound
}
