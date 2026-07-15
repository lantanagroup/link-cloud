using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.Automation.Link.Validation;

public class NormalizationDatabaseValidator
{
    private const int MaxErrors = 100;
    private readonly IAutomationOutput _output;
    private readonly PipelineDataReader _reader;

    public NormalizationDatabaseValidator(IAutomationOutput output, PipelineDataReader reader)
    {
        _output = output;
        _reader = reader;
    }

    public async Task ValidateAllAsync(string facilityId)
    {
        var errors = new List<string>();

        try
        {
            await ValidateOperations(facilityId, errors);
            await ValidateOperationResourceTypes(facilityId, errors);
            await ValidateOperationSequences(facilityId, errors);
        }
        catch (Exception ex)
        {
            AddError(errors, $"Unhandled exception during normalization DB validation: {ex.Message}");
        }

        if (errors.Count == 0)
        {
            _output.WriteLine("NORMALIZATION DATABASE VALIDATION: Passed");
            return;
        }

        _output.WriteLine($"NORMALIZATION DATABASE VALIDATION: Failed ({errors.Count} issue(s))");
        foreach (var error in errors)
        {
            _output.WriteLine($"  - {error}");
        }

        throw new InvalidOperationException($"NORMALIZATION DATABASE VALIDATION failed with {errors.Count} issue(s).");
    }

    private static void AddError(List<string> errors, string message)
    {
        if (errors.Count < MaxErrors)
            errors.Add(message);
    }

    private async Task ValidateOperations(string facilityId, List<string> errors)
    {
        var operations = await _reader.GetOperationsAsync(facilityId);

        if (operations.Count == 0)
        {
            AddError(errors, $"Expected at least 1 Operation for facility {facilityId} but found none.");
            return;
        }

        foreach (var op in operations)
        {
            if (string.IsNullOrWhiteSpace(op.OperationType)) AddError(errors, $"Operation {op.Id} OperationType should be populated.");
            if (string.IsNullOrWhiteSpace(op.OperationJson)) AddError(errors, $"Operation {op.Id} OperationJson should be populated.");
            if (op.IsDisabled) AddError(errors, $"Operation {op.Id} should not be disabled.");
        }
    }

    private async Task ValidateOperationResourceTypes(string facilityId, List<string> errors)
    {
        var operations = await _reader.GetOperationsAsync(facilityId);

        foreach (var op in operations)
        {
            if (op.ResourceTypes.Count == 0)
            {
                AddError(errors, $"Operation {op.Id} ({op.OperationType}) has no OperationResourceTypes.");
                continue;
            }

            foreach (var resourceType in op.ResourceTypes)
            {
                if (string.IsNullOrWhiteSpace(resourceType))
                    AddError(errors, $"Operation {op.Id} has an empty ResourceType name.");
            }
        }
    }

    private async Task ValidateOperationSequences(string facilityId, List<string> errors)
    {
        var sequences = await _reader.GetOperationSequencesAsync(facilityId);

        if (sequences.Count == 0)
            return;

        foreach (var seq in sequences)
        {
            if (string.IsNullOrWhiteSpace(seq.OperationType))
                AddError(errors, $"OperationSequence {seq.Id} OperationType is null.");
            if (string.IsNullOrWhiteSpace(seq.ResourceType))
                AddError(errors, $"OperationSequence {seq.Id} ResourceType is null.");
        }

        var sequenceGroups = sequences
            .Where(s => s.Sequence.HasValue)
            .GroupBy(s => s.ResourceType ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        foreach (var group in sequenceGroups)
        {
            var sequenceNumbers = group.Select(s => s.Sequence!.Value).ToList();
            if (sequenceNumbers.Distinct().Count() != sequenceNumbers.Count)
            {
                var resourceType = string.IsNullOrWhiteSpace(group.Key) ? "(unknown)" : group.Key;
                AddError(errors, $"OperationSequence values are not unique for resource type '{resourceType}'.");
            }
        }
    }
}
