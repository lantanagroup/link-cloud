namespace LantanaGroup.Link.Shared.Application.Models.Mapping;

/// <summary>
/// The result of applying one configured code map to a single patient's resources, reported on
/// <c>MappingOutcomeEvaluated</c> by Normalization.
/// </summary>
/// <remarks>
/// <para>
/// One instance per distinct (<paramref name="SourceSystem"/>, <paramref name="TargetSystem"/>) pair,
/// accumulated across every resource and operation in one correlation. Because a correlation is one
/// patient, the counts are per patient rather than per resource.
/// </para>
/// <para>
/// The producer labels the outcome by the target system it actually wrote and infers nothing about how
/// it is displayed; narrowing the open set of target systems to a fixed set of report columns is the
/// consumer's decision.
/// </para>
/// </remarks>
/// <param name="SourceSystem">
/// The code system the map reads from, as configured on the facility's code map. Carried because a
/// facility may map several local systems into the same target — without it those tallies merge and a
/// single failing source cannot be identified.
/// </param>
/// <param name="TargetSystem">
/// The code system the map writes. This is the value assigned to <c>coding.System</c>, so it is the
/// code type the normalized bundle ends up carrying rather than an inference from resource type.
/// </param>
/// <param name="Status">
/// <see cref="MappingStatus"/> projected from the counts below: no counts at all is
/// <see cref="MappingStatus.NotApplicable"/>, failures with no counts is
/// <see cref="MappingStatus.Unknown"/>, and the remaining cases follow the mapped/unmapped split.
/// </param>
/// <param name="MappedCount">Number of codings rewritten to <paramref name="TargetSystem"/>.</param>
/// <param name="UnmappedCount">
/// Number of codings that matched the map's source system but had no entry for their code. This is the
/// authoritative total; <paramref name="UnmappedCodes"/> may be a truncated sample of it.
/// </param>
/// <param name="FailureCount">
/// Number of code map operations that failed for this pair. Tracked apart from the mapped and unmapped
/// counts because a failed operation yields no counts: reporting it as unmapped would blame the
/// facility's configuration for a processing fault, and reporting it as mapped would hide the fault.
/// </param>
/// <param name="UnmappedCodes">
/// A de-duplicated, case-insensitive sample of the codes counted by <paramref name="UnmappedCount"/>,
/// intended to show which codes a facility needs to add to the map. It is not used for counting.
/// </param>
/// <param name="MappedCodes">
/// A sample of the source codes rewritten and the target codes they were rewritten to. Intended for
/// troubleshooting, not for counting.
/// </param>
public sealed record CodeMapOutcome(
    string SourceSystem,
    string TargetSystem,
    MappingStatus Status,
    int MappedCount,
    int UnmappedCount,
    int FailureCount,
    IReadOnlyList<string> UnmappedCodes,
    IReadOnlyList<CodeMapping>? MappedCodes = null);

/// <summary>A source code and the code it was rewritten to by a configured map.</summary>
public sealed record CodeMapping(string SourceCode, string TargetCode);
