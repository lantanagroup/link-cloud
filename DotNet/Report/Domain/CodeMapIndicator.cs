using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Mapping;

namespace LantanaGroup.Link.Report.Domain;

/// <summary>
/// Turns the code map outcomes reported by Normalization into the stored indicator, and combines the
/// outcomes of the passes a patient goes through.
/// </summary>
/// <remarks>
/// <para>
/// Normalization produces one message per acquisition pass, and a reportable patient goes through two --
/// an initial pass and a supplemental one after evaluation finds them reportable. Each pass reports only
/// the resources it saw, so a patient's true result is the combination of both. Deriving the status from a
/// single pass and storing it would let whichever arrived last erase the other.
/// </para>
/// <para>
/// The combination is by replacement rather than addition: each pass's contribution is stored under its
/// own identity and a pass reported again replaces its entry. Adding would be wrong under at-least-once
/// delivery, where a redelivered message repeats a pass already counted rather than introducing a new one.
/// </para>
/// </remarks>
public static class CodeMapIndicator
{
    /// <summary>
    /// Maximum distinct unmapped codes retained per (source, target) pair, matching the cap Normalization
    /// applies per message. Re-applied to the totals because combining capped lists can otherwise exceed it.
    /// </summary>
    private const int MaxUnmappedCodeSamples = 20;

    /// <summary>
    /// Records one pass's outcomes against what earlier passes stored, returning the details to store.
    /// </summary>
    /// <remarks>
    /// A pass already present is replaced, so recording the same pass twice leaves the totals unchanged.
    /// </remarks>
    public static NormalizationMappingDetails Merge(
        NormalizationMappingDetails? stored,
        string? correlationId,
        string? queryType,
        IReadOnlyList<CodeMapOutcome> codeMapOutcomes)
    {
        var passes = stored?.Passes is { Count: > 0 } storedPasses
            ? storedPasses.Where(pass => !IsSamePass(pass, correlationId, queryType)).ToList()
            : RecoverPasses(stored);

        passes.Add(new NormalizationPassDetails(correlationId, queryType, codeMapOutcomes));

        return new NormalizationMappingDetails(Total(passes), passes);
    }

    /// <summary>
    /// Two entries describe the same pass when they carry the same identity. A pass with no identity
    /// matches only another with none, so an unidentified pass replaces itself rather than accumulating.
    /// </summary>
    private static bool IsSamePass(NormalizationPassDetails pass, string? correlationId, string? queryType) =>
        string.Equals(pass.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(pass.QueryType, queryType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Rebuilds the pass list for a row written before passes were recorded, preserving its totals as one
    /// unnamed pass rather than discarding a real earlier result.
    /// </summary>
    private static List<NormalizationPassDetails> RecoverPasses(NormalizationMappingDetails? stored) =>
        stored?.CodeMaps is { Count: > 0 } codeMaps
            ? [new NormalizationPassDetails(null, null, codeMaps)]
            : [];

    /// <summary>
    /// Sums every pass's outcomes into the combined totals, one entry per (source system, target system).
    /// </summary>
    private static IReadOnlyList<CodeMapOutcome> Total(IEnumerable<NormalizationPassDetails> passes)
    {
        var totals = new Dictionary<(string SourceSystem, string TargetSystem), Tally>();

        Accumulate(totals, passes.SelectMany(pass => pass.CodeMaps));

        return totals
            .Select(entry => new CodeMapOutcome(
                entry.Key.SourceSystem,
                entry.Key.TargetSystem,
                Resolve(entry.Value.MappedCount, entry.Value.UnmappedCount, entry.Value.FailureCount),
                entry.Value.MappedCount,
                entry.Value.UnmappedCount,
                entry.Value.FailureCount,
                entry.Value.UnmappedCodes.ToList()))
            .ToList();
    }

    /// <summary>
    /// Resolves the indicator for one target system from every outcome reported against it.
    /// </summary>
    /// <remarks>
    /// Takes a sequence because a facility may map several source systems into the same target, producing
    /// one outcome per source. Their counts sum and the status comes from the totals. An empty sequence is
    /// <see cref="MappingIndicatorStatus.NotApplicable"/>: nothing was configured to write that system.
    /// </remarks>
    public static MappingIndicatorStatus Resolve(IEnumerable<CodeMapOutcome> outcomes)
    {
        var reported = false;
        var mappedCount = 0;
        var unmappedCount = 0;
        var failureCount = 0;

        foreach (var outcome in outcomes)
        {
            reported = true;
            mappedCount += outcome.MappedCount;
            unmappedCount += outcome.UnmappedCount;
            failureCount += outcome.FailureCount;
        }

        if (!reported)
        {
            return MappingIndicatorStatus.NotApplicable;
        }

        return ToIndicator(Resolve(mappedCount, unmappedCount, failureCount));
    }

    /// <summary>
    /// Resolves the HSLOC indicator, ignoring outcomes reported against any other target system.
    /// </summary>
    public static MappingIndicatorStatus ResolveHsloc(IEnumerable<CodeMapOutcome> outcomes) =>
        Resolve(outcomes.Where(outcome => MappingTargetSystems.IsHsloc(outcome.TargetSystem)));

    private static MappingStatus Resolve(int mappedCount, int unmappedCount, int failureCount)
    {
        // Nothing was counted either way. Either the code maps ran and had nothing to act on, or every one
        // of them failed -- and a processing fault must not be reported as a gap in the facility's
        // configuration, nor hidden as a success.
        if (mappedCount == 0 && unmappedCount == 0)
        {
            return failureCount > 0
                ? MappingStatus.Unknown
                : MappingStatus.NotApplicable;
        }

        if (unmappedCount == 0)
        {
            return MappingStatus.Mapped;
        }

        if (mappedCount == 0)
        {
            return MappingStatus.Unmapped;
        }

        return MappingStatus.PartiallyMapped;
    }

    /// <summary>
    /// Projects the wire status onto the stored indicator. They are separate enums because the stored one
    /// also carries states no producer reports -- <see cref="MappingIndicatorStatus.NotEvaluated"/> for a
    /// source that has not arrived, and <see cref="MappingIndicatorStatus.Assumed"/>, which only the
    /// acquisition side can produce.
    /// </summary>
    private static MappingIndicatorStatus ToIndicator(MappingStatus status) => status switch
    {
        MappingStatus.Mapped => MappingIndicatorStatus.Mapped,
        MappingStatus.PartiallyMapped => MappingIndicatorStatus.PartiallyMapped,
        MappingStatus.Unmapped => MappingIndicatorStatus.Unmapped,
        MappingStatus.Unknown => MappingIndicatorStatus.Unknown,
        _ => MappingIndicatorStatus.NotApplicable
    };

    private static void Accumulate(
        Dictionary<(string SourceSystem, string TargetSystem), Tally> merged,
        IEnumerable<CodeMapOutcome> outcomes)
    {
        foreach (var outcome in outcomes)
        {
            var key = (outcome.SourceSystem, outcome.TargetSystem);

            if (!merged.TryGetValue(key, out var tally))
            {
                tally = new Tally();
                merged[key] = tally;
            }

            tally.MappedCount += outcome.MappedCount;
            tally.UnmappedCount += outcome.UnmappedCount;
            tally.FailureCount += outcome.FailureCount;

            foreach (var code in outcome.UnmappedCodes)
            {
                if (tally.UnmappedCodes.Count >= MaxUnmappedCodeSamples)
                {
                    break;
                }

                tally.UnmappedCodes.Add(code);
            }
        }
    }

    private sealed class Tally
    {
        public int MappedCount { get; set; }
        public int UnmappedCount { get; set; }
        public int FailureCount { get; set; }
        public HashSet<string> UnmappedCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
