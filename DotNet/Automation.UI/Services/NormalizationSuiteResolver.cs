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
        {
            suite = await _store.GetSuiteByIdAsync(suiteId.Value, ct);
            if (suite == null)
                throw new InvalidOperationException($"Normalization suite '{suiteId.Value}' was not found.");
        }
        else
        {
            suite = await _store.GetDefaultSuiteAsync(ct);
        }

        if (suite == null)
            return new NormalizationSuiteResolution("None", [], [], []);

        var allOperations = await _store.GetAllOperationsAsync(ct);
        var allSequences = await _store.GetAllSequencesAsync(ct);
        var opLookup = allOperations.ToDictionary(o => o.Id);

        var resolvedOps = new List<NormalizationOperationDefinition>();
        var resolvedSequences = new List<NormalizationSuiteSequenceResolution>();
        var standaloneOperations = new List<NormalizationOperationDefinition>();

        // Add operations from sequences (in sequence order).
        foreach (var seqId in suite.SequenceIds)
        {
            var seq = allSequences.FirstOrDefault(s => s.Id == seqId);
            if (seq == null)
                throw new InvalidOperationException($"Normalization suite '{suite.Name}' references missing sequence '{seqId}'.");

            var sequenceOps = new List<NormalizationSuiteSequenceOperationResolution>();

            foreach (var entry in seq.Entries.OrderBy(e => e.Sequence))
            {
                if (opLookup.TryGetValue(entry.OperationId, out var op))
                {
                    resolvedOps.Add(op);
                    sequenceOps.Add(new NormalizationSuiteSequenceOperationResolution(entry.Sequence, op));
                }
                else
                {
                    throw new InvalidOperationException($"Normalization sequence '{seq.Name}' references missing operation '{entry.OperationId}'.");
                }
            }

            resolvedSequences.Add(new NormalizationSuiteSequenceResolution(seq.Name, sequenceOps));
        }

        // Add standalone operations from the suite.
        foreach (var opId in suite.OperationIds)
        {
            if (opLookup.TryGetValue(opId, out var op))
            {
                resolvedOps.Add(op);
                standaloneOperations.Add(op);
            }
            else
            {
                throw new InvalidOperationException($"Normalization suite '{suite.Name}' references missing standalone operation '{opId}'.");
            }
        }

        return new NormalizationSuiteResolution(suite.Name, resolvedOps, resolvedSequences, standaloneOperations);
    }
}

/// <summary>
/// The result of resolving a normalization suite.
/// </summary>
public record NormalizationSuiteResolution(
    string SuiteName,
    List<NormalizationOperationDefinition> Operations,
    List<NormalizationSuiteSequenceResolution> Sequences,
    List<NormalizationOperationDefinition> StandaloneOperations);

public record NormalizationSuiteSequenceResolution(
    string SequenceName,
    List<NormalizationSuiteSequenceOperationResolution> Operations);

public record NormalizationSuiteSequenceOperationResolution(
    int Sequence,
    NormalizationOperationDefinition Operation);
