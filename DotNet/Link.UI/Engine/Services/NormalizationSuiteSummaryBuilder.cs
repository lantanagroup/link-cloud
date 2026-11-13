using Automation.UI.Models;

namespace Automation.UI.Services;

/// <summary>
/// Builds the read-only suite and organization-map summaries shown from run details.
/// A missing operation or sequence is reported on the summary. It does not fail the page.
/// </summary>
public static class NormalizationSuiteSummaryBuilder
{
    public static NormalizationSuiteSummary Build(
        NormalizationSuiteDefinition suite,
        IReadOnlyList<NormalizationOperationDefinition> operations,
        IReadOnlyList<NormalizationSequenceDefinition> sequences)
    {
        var opLookup = new Dictionary<Guid, NormalizationOperationDefinition>();
        foreach (var operation in operations)
        {
            if (!opLookup.ContainsKey(operation.Id))
                opLookup[operation.Id] = operation;
        }

        var sequenceLookup = new Dictionary<Guid, NormalizationSequenceDefinition>();
        foreach (var sequence in sequences)
        {
            if (!sequenceLookup.ContainsKey(sequence.Id))
                sequenceLookup[sequence.Id] = sequence;
        }

        var sequenceSummaries = new List<NormalizationSuiteSummarySequence>();
        foreach (var sequenceId in suite.SequenceIds)
        {
            if (!sequenceLookup.TryGetValue(sequenceId, out var sequence))
            {
                sequenceSummaries.Add(new NormalizationSuiteSummarySequence
                {
                    Name = "Missing sequence",
                    Operations =
                    [
                        new NormalizationSuiteSummaryOperation
                        {
                            Name = sequenceId.ToString(),
                            Missing = true
                        }
                    ]
                });
                continue;
            }

            var steps = new List<NormalizationSuiteSummaryOperation>();
            foreach (var entry in sequence.Entries.OrderBy(e => e.Sequence))
                steps.Add(ToOperation(entry.Sequence, entry.OperationId, opLookup));

            sequenceSummaries.Add(new NormalizationSuiteSummarySequence
            {
                Name = sequence.Name,
                Operations = steps
            });
        }

        var standalone = new List<NormalizationSuiteSummaryOperation>();
        foreach (var operationId in suite.OperationIds)
            standalone.Add(ToOperation(0, operationId, opLookup));

        return new NormalizationSuiteSummary
        {
            Id = suite.Id,
            Name = suite.Name,
            Description = suite.Description,
            IsSystem = suite.IsSystem,
            IsDefault = suite.IsDefault,
            Sequences = sequenceSummaries,
            Operations = standalone
        };
    }

    public static OrganizationMapSummary Build(OrganizationResourceMapTemplate template)
    {
        return new OrganizationMapSummary
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            IsSystem = template.IsSystem,
            IsDefault = template.IsDefault,
            Conditions = template.Conditions
                .OrderBy(c => c.Priority)
                .Select(c => new OrganizationMapConditionSummary
                {
                    Priority = c.Priority,
                    FhirPath = c.FhirPath ?? string.Empty
                })
                .ToList()
        };
    }

    private static NormalizationSuiteSummaryOperation ToOperation(
        int sequence,
        Guid operationId,
        IReadOnlyDictionary<Guid, NormalizationOperationDefinition> opLookup)
    {
        if (!opLookup.TryGetValue(operationId, out var operation))
        {
            return new NormalizationSuiteSummaryOperation
            {
                Sequence = sequence,
                Name = operationId.ToString(),
                Missing = true
            };
        }

        return new NormalizationSuiteSummaryOperation
        {
            Sequence = sequence,
            Name = operation.Name,
            OperationType = operation.OperationType,
            ResourceTypes = operation.ResourceTypes?.Where(r => !string.IsNullOrWhiteSpace(r)).ToList() ?? []
        };
    }
}
