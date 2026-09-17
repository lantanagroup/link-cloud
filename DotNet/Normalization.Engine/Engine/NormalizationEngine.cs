using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Operations;
using LantanaGroup.Link.Normalization.Application.Operations;
using LantanaGroup.Link.Normalization.Application.Services.Operations;

namespace LantanaGroup.Link.Normalization.Engine;

/// <summary>
/// In-process runner for the same operation implementations the Normalization service uses.
/// No Kafka, tenant, or database dependency.
/// </summary>
public sealed class NormalizationEngine(
    CopyPropertyOperationService copyProperty,
    CodeMapOperationService codeMap,
    ConditionalTransformOperationService conditionalTransform,
    CopyLocationOperationService copyLocation,
    RemoveExtensionsOperationService removeExtensions,
    CopyLocationAliasToTypeIterativelyOperationService copyLocationAlias)
{
    public async Task<IReadOnlyList<NormalizationStepOutcome>> ApplyAsync(
        DomainResource resource,
        IReadOnlyList<NormalizationWorkItem> steps,
        IReadOnlyList<DomainResource>? supportingResources = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var outcomes = new List<NormalizationStepOutcome>();
        if (steps == null || steps.Count == 0)
            return outcomes;

        var ordered = steps
            .Where(s => s.Operation != null)
            .Where(s => AppliesTo(s, resource.TypeName))
            .OrderBy(s => s.Sequence)
            .ToList();

        var support = supportingResources?.ToList();

        foreach (var step in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ProcessAsync(step.Operation, resource, support, cancellationToken);
            outcomes.Add(new NormalizationStepOutcome(step.Sequence, step.Operation, result));
        }

        return outcomes;
    }

    public async System.Threading.Tasks.Task ApplyAllAsync(
        IReadOnlyList<DomainResource> resources,
        IReadOnlyList<NormalizationWorkItem> steps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var support = resources.ToList();
        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ApplyAsync(resource, steps, support, cancellationToken);
        }
    }

    private async Task<OperationResult> ProcessAsync(
        IOperation operation,
        DomainResource resource,
        List<DomainResource>? supportingResources,
        CancellationToken cancellationToken)
        => operation.OperationType switch
        {
            OperationType.CopyProperty =>
                await copyProperty.ProcessOperationAsync((CopyPropertyOperation)operation, resource, cancellationToken: cancellationToken),
            OperationType.CodeMap =>
                await codeMap.ProcessOperationAsync((CodeMapOperation)operation, resource, cancellationToken: cancellationToken),
            OperationType.ConditionalTransform =>
                await conditionalTransform.ProcessOperationAsync((ConditionalTransformOperation)operation, resource, cancellationToken: cancellationToken),
            OperationType.CopyLocation =>
                await copyLocation.ProcessOperationAsync((CopyLocationOperation)operation, resource, cancellationToken: cancellationToken),
            OperationType.RemoveExtensions =>
                await removeExtensions.ProcessOperationAsync((RemoveExtensionsOperation)operation, resource, cancellationToken: cancellationToken),
            OperationType.CopyLocationAliasToTypeIteratively =>
                await copyLocationAlias.ProcessOperationAsync(
                    (CopyLocationAliasToTypeIterativelyOperation)operation,
                    resource,
                    supportingResources?.OfType<Location>().Cast<DomainResource>().ToList(),
                    cancellationToken),
            _ => OperationResult.Failure($"Unsupported operation type '{operation.OperationType}'.", resource)
        };

    private static bool AppliesTo(NormalizationWorkItem step, string resourceType)
    {
        if (step.ResourceTypes == null || step.ResourceTypes.Count == 0)
            return true;
        return step.ResourceTypes.Any(t => string.Equals(t, resourceType, StringComparison.OrdinalIgnoreCase));
    }
}
