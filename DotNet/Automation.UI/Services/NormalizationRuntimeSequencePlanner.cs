using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Compiles an Automation suite into the per-resource-type sequence numbers
/// the Normalization service actually runs and logs. Suite sequences can restart
/// at 1 (and can mix resource types); runtime sequences are 1..N per type, in
/// the same flatten order <c>RunExecutor</c> uses when calling the API.
/// </summary>
internal static class NormalizationRuntimeSequencePlanner
{
    public static List<PlannedRuntimeStep> Plan(NormalizationSuiteResolution resolution)
    {
        var planned = new List<PlannedRuntimeStep>();
        var nextByResourceType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var sequence in resolution.Sequences)
        {
            foreach (var op in sequence.Operations.OrderBy(o => o.Sequence))
                Add(sequence.SequenceName, op.Sequence, op.Operation);
        }

        foreach (var op in resolution.StandaloneOperations)
            Add("(standalone)", 0, op);

        return planned;

        void Add(string sequenceName, int suiteSequence, NormalizationOperationDefinition operation)
        {
            var resourceTypes = operation.ResourceTypes
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (resourceTypes.Length == 0)
            {
                planned.Add(new PlannedRuntimeStep(sequenceName, suiteSequence, 0, string.Empty, operation));
                return;
            }

            foreach (var resourceType in resourceTypes)
            {
                nextByResourceType.TryGetValue(resourceType, out var current);
                var runtimeSequence = current + 1;
                nextByResourceType[resourceType] = runtimeSequence;
                planned.Add(new PlannedRuntimeStep(sequenceName, suiteSequence, runtimeSequence, resourceType, operation));
            }
        }
    }
}

internal sealed record PlannedRuntimeStep(
    string SequenceName,
    int SuiteSequence,
    int RuntimeSequence,
    string ResourceType,
    NormalizationOperationDefinition Operation)
{
    public string DisplayName =>
        string.IsNullOrWhiteSpace(ResourceType)
            ? $"{SequenceName}#{SuiteSequence}"
            : $"{SequenceName}#{SuiteSequence} (runtime {ResourceType}#{RuntimeSequence})";
}
