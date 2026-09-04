namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// One location a patient's encounters referenced during acquisition, and whether it resolved to the
/// reporting organization.
/// </summary>
/// <remarks>
/// Carried so a reviewer looking at an unresolved patient can see which locations were actually
/// considered and why they were rejected, rather than only that the patient did not resolve. Every
/// field is projected from the <c>OrganizationLocationMapping</c> row the encounter already joined to.
/// </remarks>
/// <param name="LocationId">
/// The id of the FHIR <c>Location</c> the encounter referenced. Bare, with any resource-type prefix
/// stripped.
/// </param>
/// <param name="LocationName">The Location's <c>name</c>, for display.</param>
/// <param name="LocationAlias">
/// The Location's first <c>alias</c>, falling back to its name when it carries none. This is the field
/// NHSN location codes ride on, so it is usually the value that matters for troubleshooting rather than
/// <paramref name="LocationName"/>.
/// </param>
/// <param name="PartOfValue">
/// The bare id of the Location's parent, from <c>Location.partOf</c>, or null at the root of the
/// hierarchy. Names the managing location a child inherited its org membership from.
/// </param>
/// <param name="IsOrgLocation">
/// Whether the location resolved to the reporting organization. True when the location matched the
/// facility's own org-location conditions <b>or</b> inherited membership from an ancestor — the two are
/// collapsed into one value here, so this does not distinguish a direct match from an inherited one.
/// </param>
public sealed record LocationOrgMatch(
    string LocationId,
    string? LocationName,
    string? LocationAlias,
    string? PartOfValue,
    bool IsOrgLocation);
