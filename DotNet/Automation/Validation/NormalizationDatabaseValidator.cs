using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

public class NormalizationDatabaseValidator
{
    private const int MaxErrors = 100;
    private readonly ITestOutputHelper _output;
    private readonly PipelineDataReader _reader;

    public NormalizationDatabaseValidator(ITestOutputHelper output, DatabaseConnectionFactory dbFactory)
    {
        _output = output;
        _reader = new PipelineDataReader(dbFactory);
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
            if (op.OperationResourceTypes.Count == 0)
            {
                AddError(errors, $"Operation {op.Id} ({op.OperationType}) has no OperationResourceTypes.");
                continue;
            }

            foreach (var ort in op.OperationResourceTypes)
            {
                if (ort.ResourceType == null)
                    AddError(errors, $"OperationResourceType {ort.Id} has null ResourceType.");
                else if (string.IsNullOrWhiteSpace(ort.ResourceType.Name))
                    AddError(errors, $"OperationResourceType {ort.Id} ResourceType.Name should be populated.");
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
            if (seq.OperationResourceType == null)
            {
                AddError(errors, $"OperationSequence {seq.Id} OperationResourceType is null.");
                continue;
            }

            if (seq.OperationResourceType.Operation == null)
                AddError(errors, $"OperationSequence {seq.Id} OperationResourceType.Operation is null.");
            if (seq.OperationResourceType.ResourceType == null)
                AddError(errors, $"OperationSequence {seq.Id} OperationResourceType.ResourceType is null.");
        }

        var sequenceNumbers = sequences.Where(s => s.Sequence.HasValue).Select(s => s.Sequence!.Value).ToList();
        if (sequenceNumbers.Distinct().Count() != sequenceNumbers.Count)
            AddError(errors, "OperationSequence values are not unique.");
    }
}
