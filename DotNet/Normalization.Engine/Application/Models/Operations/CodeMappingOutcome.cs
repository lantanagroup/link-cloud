namespace LantanaGroup.Link.Normalization.Application.Models.Operations
{
    /// <summary>
    /// What one configured code map did to a single resource: how many of its codings were rewritten, how
    /// many had no entry, and which codes those were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Produced only by <c>CodeMapOperationService</c>; every other operation type leaves
    /// <see cref="OperationResult.CodeMapping"/> null. One instance per <c>CodeSystemMap</c> the operation
    /// actually exercised — a map whose source system no coding used produces nothing at all, which is
    /// distinct from one that matched and found no codes.
    /// </para>
    /// <para>
    /// Labelled by the map's own configured systems rather than by the coding's, because the operation
    /// rewrites <c>coding.System</c> as it goes and a coding inspected afterwards no longer reports the
    /// system it was matched on.
    /// </para>
    /// </remarks>
    /// <param name="SourceSystem">The code system the map reads from, as configured.</param>
    /// <param name="TargetSystem">The code system the map writes, as configured.</param>
    /// <param name="MappedCount">Codings rewritten to <paramref name="TargetSystem"/>.</param>
    /// <param name="UnmappedCount">
    /// Codings that used the map's source system but had no entry for their code. Counts every occurrence,
    /// so it does not match <paramref name="UnmappedCodes"/> when one code appears more than once.
    /// </param>
    /// <param name="UnmappedCodes">
    /// The distinct codes behind <paramref name="UnmappedCount"/> — the codes a facility would add to the
    /// map to close the gap.
    /// </param>
    public sealed record CodeMappingOutcome(
        string SourceSystem,
        string TargetSystem,
        int MappedCount,
        int UnmappedCount,
        IReadOnlyList<string> UnmappedCodes);
}
