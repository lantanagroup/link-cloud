using LantanaGroup.Link.Shared.Application.Models.Mapping;

namespace LantanaGroup.Link.Report.Domain.Models;

/// <summary>
/// The shape stored in <c>ReportEntryMappingOutcome.AcquisitionDetails</c>.
/// </summary>
/// <remarks>
/// Replaced wholesale by each acquisition message rather than appended to: a patient passes through an
/// initial pass and possibly a re-acquisition, and only the latest describes the bundle that was evaluated.
/// </remarks>
/// <param name="LocationOrg">The org-location resolution behind the two acquisition-owned columns.</param>
public sealed record AcquisitionMappingDetails(LocationOrgDetails LocationOrg);

/// <summary>
/// The counts and matched locations behind the Location Org and Encounter Mapping columns.
/// </summary>
/// <remarks>
/// Carries what the status collapsed. The columns say something is wrong; these say how much and where.
/// </remarks>
/// <param name="Status">The producer's own coarse result, before the report-side refinement.</param>
/// <param name="EncounterCount">Encounters acquired for this patient in this correlation.</param>
/// <param name="OrgEncounterCount">Of those, how many resolved to the reporting organization.</param>
/// <param name="AssumedOrgEncounterCount">
/// Of those, how many were accepted only because they carried no resolvable location references, and so
/// were never verified against the facility's configuration.
/// </param>
/// <param name="Matches">
/// The distinct locations the encounters referenced. Includes the ones that did not resolve — those are
/// what a user would go and configure, so they are the actionable half.
/// </param>
public sealed record LocationOrgDetails(
    LocationOrgStatus Status,
    int EncounterCount,
    int OrgEncounterCount,
    int AssumedOrgEncounterCount,
    IReadOnlyList<LocationOrgMatch> Matches);

/// <summary>
/// The shape stored in <c>ReportEntryMappingOutcome.NormalizationDetails</c>.
/// </summary>
/// <param name="CodeMaps">
/// Every code map the patient's resources exercised, including target systems no column recognizes. Without
/// them a facility with a mistyped system would be invisible outside the logs.
/// </param>
public sealed record NormalizationMappingDetails(IReadOnlyList<CodeMapOutcome> CodeMaps);
