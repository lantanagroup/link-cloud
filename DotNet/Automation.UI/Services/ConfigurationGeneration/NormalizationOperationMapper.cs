using Automation.UI.Models;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Engine;

namespace Automation.UI.Services.ConfigurationGeneration;

internal static class NormalizationOperationMapper
{
    public static List<NormalizationWorkItem> ToWorkItems(
        IReadOnlyList<NormalizationOperationDefinition> existingOps,
        IReadOnlyList<GeneratedNormalizationOperationProposal> proposedOps)
    {
        var items = new List<NormalizationWorkItem>();
        var sequence = 1;

        foreach (var op in existingOps)
        {
            var engineOp = FromDefinition(op);
            if (engineOp == null)
                continue;
            items.Add(new NormalizationWorkItem(sequence++, engineOp, op.ResourceTypes));
        }

        foreach (var proposed in proposedOps)
        {
            var engineOp = FromProposal(proposed);
            if (engineOp == null)
                continue;
            items.Add(new NormalizationWorkItem(sequence++, engineOp, proposed.ResourceTypes));
        }

        return items;
    }

    public static IOperation? FromDefinition(NormalizationOperationDefinition op)
        => Create(
            op.OperationType,
            op.Name,
            op.Description,
            op.SourceFhirPath,
            op.TargetFhirPath,
            op.ConditionTargetFhirPath,
            op.ConditionTargetValue,
            op.Conditions,
            op.CodeMapFhirPath,
            op.CodeSystemMaps,
            op.ExtensionUrls,
            op.MaxIterations,
            op.SplitOnComma);

    public static IOperation? FromProposal(GeneratedNormalizationOperationProposal op)
        => Create(
            op.OperationType,
            op.SuggestedName,
            op.SuggestedDescription,
            op.SourceFhirPath,
            op.TargetFhirPath,
            op.ConditionTargetFhirPath,
            op.ConditionTargetValue,
            op.Conditions,
            op.CodeMapFhirPath,
            op.CodeSystemMaps,
            op.ExtensionUrls,
            op.MaxIterations,
            op.SplitOnComma);

    private static IOperation? Create(
        string operationType,
        string name,
        string? description,
        string? sourceFhirPath,
        string? targetFhirPath,
        string? conditionTargetFhirPath,
        object? conditionTargetValue,
        IReadOnlyList<NormalizationCondition>? conditions,
        string? codeMapFhirPath,
        IReadOnlyList<NormalizationCodeSystemMap>? codeSystemMaps,
        IReadOnlyList<string>? extensionUrls,
        int maxIterations,
        bool splitOnComma)
    {
        if (string.IsNullOrWhiteSpace(operationType))
            return null;

        try
        {
            if (operationType.Equals("CopyLocation", StringComparison.OrdinalIgnoreCase))
                return new CopyLocationOperation { Name = name, Description = description ?? "" };

            if (operationType.Equals("CopyLocationAliasToTypeIteratively", StringComparison.OrdinalIgnoreCase))
            {
                return new CopyLocationAliasToTypeIterativelyOperation
                {
                    Name = name,
                    Description = description ?? "",
                    MaxIterations = maxIterations <= 0 ? 15 : maxIterations,
                    SplitOnComma = splitOnComma
                };
            }

            if (operationType.Equals("RemoveExtensions", StringComparison.OrdinalIgnoreCase))
            {
                return new RemoveExtensionsOperation
                {
                    Name = name,
                    Description = description ?? "",
                    ExtensionUrls = extensionUrls?.ToList() ?? []
                };
            }

            if (operationType.Equals("CopyProperty", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(sourceFhirPath)
                && !string.IsNullOrWhiteSpace(targetFhirPath))
            {
                return new CopyPropertyOperation(name, sourceFhirPath, targetFhirPath, description ?? "");
            }

            if (operationType.Equals("ConditionalTransform", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(conditionTargetFhirPath ?? targetFhirPath))
            {
                var transformConditions = (conditions ?? [])
                    .Select(ToEngineCondition)
                    .ToList();
                return new ConditionalTransformOperation(
                    name,
                    conditionTargetFhirPath ?? targetFhirPath!,
                    conditionTargetValue ?? "",
                    transformConditions,
                    description ?? "");
            }

            if (operationType.Equals("CodeMap", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(codeMapFhirPath)
                && codeSystemMaps is { Count: > 0 })
            {
                var maps = codeSystemMaps.Select(ToEngineCodeSystemMap).ToList();
                return new CodeMapOperation(name, codeMapFhirPath, maps, description ?? "");
            }
        }
        catch (ArgumentException)
        {
            return null;
        }

        return null;
    }

    private static TransformCondition ToEngineCondition(NormalizationCondition condition)
    {
        var op = Enum.TryParse<ConditionOperator>(condition.Operator, ignoreCase: true, out var parsed)
            ? parsed
            : ConditionOperator.Equal;
        return new TransformCondition(condition.FhirPathSource, op, condition.Value);
    }

    private static CodeSystemMap ToEngineCodeSystemMap(NormalizationCodeSystemMap map)
    {
        var codes = new Dictionary<string, CodeMap>(StringComparer.Ordinal);
        foreach (var (key, entry) in map.CodeMaps ?? [])
        {
            codes[key] = new CodeMap(entry.Code, entry.Display ?? entry.Code);
        }

        return new CodeSystemMap(map.SourceSystem, map.TargetSystem, codes);
    }
}
