using Automation.UI.Models;
using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Resolves the normalization suite for a run from a stored suite id,
/// falling back to the system default suite.
/// </summary>
public sealed class NormalizationSuiteResolver
{
    private readonly INormalizationStore _store;

    public NormalizationSuiteResolver(INormalizationStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Resolves the suite and expands it into the full set of operations to apply.
    /// Returns the resolved operations in sequence order.
    /// </summary>
    public async Task<NormalizationSuiteResolution> ResolveAsync(Guid? suiteId, CancellationToken ct = default)
    {
        NormalizationSuiteDefinition? suite = null;

        if (suiteId.HasValue)
            suite = await _store.GetSuiteByIdAsync(suiteId.Value, ct);

        suite ??= await _store.GetDefaultSuiteAsync(ct);

        if (suite == null)
            return new NormalizationSuiteResolution("None", []);

        var allOperations = await _store.GetAllOperationsAsync(ct);
        var allSequences = await _store.GetAllSequencesAsync(ct);
        var opLookup = allOperations.ToDictionary(o => o.Id);

        var resolvedOps = new List<NormalizationOperationDefinition>();

        // Add operations from sequences (in sequence order).
        foreach (var seqId in suite.SequenceIds)
        {
            var seq = allSequences.FirstOrDefault(s => s.Id == seqId);
            if (seq == null) continue;

            foreach (var entry in seq.Entries.OrderBy(e => e.Sequence))
            {
                if (opLookup.TryGetValue(entry.OperationId, out var op))
                    resolvedOps.Add(op);
            }
        }

        // Add standalone operations from the suite.
        foreach (var opId in suite.OperationIds)
        {
            if (opLookup.TryGetValue(opId, out var op))
                resolvedOps.Add(op);
        }

        return new NormalizationSuiteResolution(suite.Name, resolvedOps);
    }
}

/// <summary>
/// The result of resolving a normalization suite.
/// </summary>
public record NormalizationSuiteResolution(string SuiteName, List<NormalizationOperationDefinition> Operations);
