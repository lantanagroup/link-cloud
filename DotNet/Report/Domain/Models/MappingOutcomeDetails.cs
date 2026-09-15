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
/// The Normalization side of a patient's stored mapping outcome, as stored in
/// <c>ReportEntryMappingOutcome.NormalizationDetails</c>.
/// </summary>
/// <param name="CodeMaps">
/// The combined totals across every pass, which is what the indicator is derived from and what a reader
/// wants. Denormalized rather than summed on read so the stored status and the stored counts cannot
/// disagree.
/// </param>
/// <param name="Passes">
/// Each pass's own contribution, retained so the combination is idempotent: a redelivered pass replaces
/// its entry rather than adding to the totals a second time.
/// </param>
public sealed record NormalizationMappingDetails(
    IReadOnlyList<CodeMapOutcome> CodeMaps,
    IReadOnlyList<NormalizationPassDetails> Passes);

/// <summary>
/// One acquisition pass's code map outcomes, identified by the pass that produced them.
/// </summary>
/// <remarks>
/// The correlation id alone does not identify a pass -- a patient's initial and supplemental acquisitions
/// share it -- so the query type is part of the identity. Both are nullable because a message produced
/// before this field existed carries neither, and such a message is treated as a single unnamed pass.
/// </remarks>
public sealed record NormalizationPassDetails(
    string? CorrelationId,
    string? QueryType,
    IReadOnlyList<CodeMapOutcome> CodeMaps);
