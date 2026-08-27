using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Shared.Application.Models.Mapping;

namespace LantanaGroup.Link.Normalization.Application.Models;

/// <summary>
/// Accumulates code-map outcomes across every resource and operation in one correlation, so the
/// per-patient result can be reported on <c>MappingOutcomeEvaluated</c>.
/// </summary>
/// <remarks>
/// One correlation is one patient, so an instance scoped to a single <c>ResourcesAcquired</c> message
/// yields the per-patient answer. Not thread-safe: the listener's resource/operation loops are
/// sequential, and adding synchronization would cost more than it buys.
/// </remarks>
public sealed class MappingOutcomeAccumulator
{
    /// <summary>
    /// Maximum distinct unmapped codes retained per (source, target) pair. The true total is always
    /// carried in <c>UnmappedCount</c>; the list is a troubleshooting sample, and a facility whose
    /// code map is empty would otherwise put every code it saw on the wire.
    /// </summary>
    private const int MaxUnmappedCodeSamples = 20;

    private readonly Dictionary<(string SourceSystem, string TargetSystem), Tally> _tallies = new();

    /// <summary>
    /// Records the outcomes of one code-map operation against one resource.
    /// </summary>
    public void Add(IReadOnlyList<CodeMappingOutcome>? outcomes)
    {
        if (outcomes is null)
        {
            // A code map that ran but matched nothing at its FHIRPath returns NoAction with no
            // outcomes. That is not a failure and not an unmapped code -- the resource simply had
            // nothing to map -- so no tally moves.
            return;
        }

        foreach (var outcome in outcomes)
        {
            var tally = GetOrCreate(outcome.SourceSystem, outcome.TargetSystem);

            tally.MappedCount += outcome.MappedCount;
            tally.UnmappedCount += outcome.UnmappedCount;

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

    /// <summary>
    /// Records that a code-map operation threw or returned <see cref="OperationStatus.Failure"/>.
    /// </summary>
    /// <remarks>
    /// Tracked separately because a failed operation produces no counts: reporting the pair as
    /// unmapped would blame the facility's configuration for what is a processing fault, and
    /// reporting it as mapped would hide the fault entirely. A thrown operation yields no per-coding
    /// outcomes, so the pairs come from the operation's own configured
    /// <c>CodeSystemMaps</c> rather than from results.
    /// </remarks>
    public void AddFailure(CodeMapOperation operation)
    {
        foreach (var map in operation.CodeSystemMaps)
        {
            GetOrCreate(map.SourceSystem, map.TargetSystem).FailureCount++;
        }
    }

    /// <summary>
    /// Projects every accumulated tally into the outcomes carried on the message.
    /// </summary>
    public IReadOnlyList<CodeMapOutcome> BuildAll() =>
        _tallies.Select(kv =>
        {
            var (key, tally) = kv;

            var status = tally switch
            {
                { MappedCount: 0, UnmappedCount: 0, FailureCount: > 0 } => MappingStatus.Unknown,
                { MappedCount: 0, UnmappedCount: 0 } => MappingStatus.NotApplicable,
                { UnmappedCount: 0 } => MappingStatus.Mapped,
                { MappedCount: 0 } => MappingStatus.Unmapped,
                _ => MappingStatus.PartiallyMapped
            };

            return new CodeMapOutcome(
                key.SourceSystem,
                key.TargetSystem,
                status,
                tally.MappedCount,
                tally.UnmappedCount,
                tally.FailureCount,
                tally.UnmappedCodes.ToList());
        }).ToList();

    private Tally GetOrCreate(string sourceSystem, string targetSystem)
    {
        var key = (sourceSystem, targetSystem);

        if (!_tallies.TryGetValue(key, out var tally))
        {
            tally = new Tally();
            _tallies[key] = tally;
        }

        return tally;
    }

    private sealed class Tally
    {
        public int MappedCount { get; set; }
        public int UnmappedCount { get; set; }
        public int FailureCount { get; set; }
        public HashSet<string> UnmappedCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}