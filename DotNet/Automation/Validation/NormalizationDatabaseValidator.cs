using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Automation.Validation;

/// <summary>
/// Validates the Normalization service's database state after a smoke test run.
/// </summary>
public class NormalizationDatabaseValidator
{
    private readonly ITestOutputHelper _output;
    private readonly DatabaseConnectionFactory _dbFactory;

    public NormalizationDatabaseValidator(ITestOutputHelper output, DatabaseConnectionFactory dbFactory)
    {
        _output = output;
        _dbFactory = dbFactory;
    }

    public async Task ValidateAllAsync(string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("=================================================================================");
        _output.WriteLine("  NORMALIZATION DATABASE VALIDATION");
        _output.WriteLine($"  FacilityId: {facilityId}");
        _output.WriteLine("=================================================================================");

        await using var db = _dbFactory.CreateNormalizationDbContext();

        await ValidateOperations(db, facilityId);
        await ValidateOperationResourceTypes(db, facilityId);
        await ValidateOperationSequences(db, facilityId);

        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("  NORMALIZATION DATABASE VALIDATION COMPLETE");
        _output.WriteLine("---------------------------------------------------------------------------------");
        _output.WriteLine("");
    }

    private async Task ValidateOperations(NormalizationDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- Operation ---");

        var operations = await PipelineSnapshot.GetOperationsAsync(db, facilityId);

        Assert.True(operations.Count > 0,
            $"Expected at least 1 Operation for FacilityId={facilityId} but found none");

        foreach (var op in operations)
        {
            Assert.False(string.IsNullOrWhiteSpace(op.OperationType),
                $"OperationType should be set for Operation Id={op.Id}");
            Assert.False(string.IsNullOrWhiteSpace(op.OperationJson),
                $"OperationJson should be set for Operation Id={op.Id}");
            Assert.False(op.IsDisabled,
                $"Operation Id={op.Id} should not be disabled");

            var resourceTypes = op.OperationResourceTypes
                .Select(ort => ort.ResourceType?.Name ?? "(unknown)")
                .ToList();

            _output.WriteLine($"      Id            = {op.Id}");
            _output.WriteLine($"        Type          = {op.OperationType}");
            _output.WriteLine($"        Name          = {op.Name}");
            _output.WriteLine($"        ResourceTypes = [{string.Join(", ", resourceTypes)}]");
            _output.WriteLine($"        Disabled      = {op.IsDisabled}");
        }

        _output.WriteLine("  --- Operation PASSED ---");
    }

    private async Task ValidateOperationResourceTypes(NormalizationDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- OperationResourceType ---");

        var operations = await PipelineSnapshot.GetOperationsAsync(db, facilityId);

        foreach (var op in operations)
        {
            Assert.True(op.OperationResourceTypes.Count > 0,
                $"Expected at least 1 OperationResourceType for Operation Id={op.Id} " +
                $"(Type={op.OperationType}) but found none");

            foreach (var ort in op.OperationResourceTypes)
            {
                Assert.NotNull(ort.ResourceType);
                Assert.False(string.IsNullOrWhiteSpace(ort.ResourceType.Name),
                    $"ResourceType.Name should be set for OperationResourceType Id={ort.Id}");

                _output.WriteLine($"      Operation {op.Id} -> ResourceType={ort.ResourceType.Name} (OrtId={ort.Id})");
            }
        }

        _output.WriteLine("  --- OperationResourceType PASSED ---");
    }

    private async Task ValidateOperationSequences(NormalizationDbContext db, string facilityId)
    {
        _output.WriteLine("");
        _output.WriteLine("  --- OperationSequence ---");

        var sequences = await PipelineSnapshot.GetOperationSequencesAsync(db, facilityId);

        if (sequences.Count == 0)
        {
            _output.WriteLine("      No OperationSequence rows found (sequences are optional)");
            _output.WriteLine("  --- OperationSequence PASSED ---");
            return;
        }

        foreach (var seq in sequences)
        {
            Assert.NotNull(seq.OperationResourceType);
            Assert.NotNull(seq.OperationResourceType.Operation);
            Assert.NotNull(seq.OperationResourceType.ResourceType);

            var opType = seq.OperationResourceType.Operation.OperationType;
            var resType = seq.OperationResourceType.ResourceType.Name;

            _output.WriteLine($"      Id={seq.Id}: Sequence={seq.Sequence}, OperationType={opType}, ResourceType={resType}");
        }

        var sequenceNumbers = sequences
            .Where(s => s.Sequence.HasValue)
            .Select(s => s.Sequence!.Value)
            .ToList();

        var distinctCount = sequenceNumbers.Distinct().Count();
        Assert.Equal(sequenceNumbers.Count, distinctCount);

        _output.WriteLine($"      {sequences.Count} sequence(s) with unique ordering");
        _output.WriteLine("  --- OperationSequence PASSED ---");
    }
}
