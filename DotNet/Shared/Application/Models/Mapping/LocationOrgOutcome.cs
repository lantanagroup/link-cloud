namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// How one patient's encounters resolved against the facility's org-location configuration during
/// acquisition, reported on <c>MappingOutcomeEvaluated</c> by DataAcquisition.
/// </summary>
/// <remarks>
/// <para>
/// Computed from the patient's <c>EncounterMapping</c> rows at the point the acquisition cache is
/// filtered, so it describes the encounter set the report was actually built from. Those rows are
/// re-scored when org-location configuration is saved, which is why the outcome is recorded here rather
/// than read back later — a later read would answer for the current configuration, not the one the
/// report ran under.
/// </para>
/// <para>
/// The counts drive two separate report indicators: whether the patient belongs to the reporting
/// organization, and whether the patient's encounters carried resolvable locations at all.
/// </para>
/// </remarks>
/// <param name="Status">
/// The org-membership result. Consumers needing to distinguish a verified patient from an assumed one
/// read the counts below instead.
/// </param>
/// <param name="EncounterCount">
/// Total encounters mapped for the patient at the facility, whether or not they resolved to the
/// organization. Zero means the patient had no encounters, which is
/// <see cref="LocationOrgStatus.NotApplicable"/> rather than a negative result.
/// </param>
/// <param name="OrgEncounterCount">
/// Encounters that resolved to the reporting organization. These are the encounters kept in the
/// acquisition cache; the remainder are stripped before the bundle goes downstream.
/// </param>
/// <param name="AssumedOrgEncounterCount">
/// Encounters counted in <paramref name="OrgEncounterCount"/> only because they carried no resolvable
/// location references. Acquisition assumes org membership in that case, which is permissive in exactly
/// the multi-facility scenario org-location resolution exists to guard against, so the count is reported
/// separately to let a consumer flag membership that was never actually verified.
/// <para>
/// Always a subset of <paramref name="OrgEncounterCount"/> — both count only encounters mapped to the
/// org, and the no-references branch is the one that marks them so. A patient whose membership was
/// wholly assumed therefore has these two counts equal and non-zero; it is not signalled by an
/// <paramref name="OrgEncounterCount"/> of zero, which cannot occur alongside a non-zero assumed count.
/// </para>
/// </param>
/// <param name="Matches">
/// The distinct locations the patient's encounters referenced, each with its resolution result. Bounded
/// by how many locations one patient touched.
/// </param>
public sealed record LocationOrgOutcome(
    LocationOrgStatus Status,
    int EncounterCount,
    int OrgEncounterCount,
    int AssumedOrgEncounterCount,
    IReadOnlyList<LocationOrgMatch> Matches);
