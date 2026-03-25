using LantanaGroup.Link.Normalization.Domain.Entities;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using Xunit;

namespace LantanaGroup.Link.Tests.E2ETests.Validation;

/// <summary>
/// Validates the Normalization service's database state after a smoke test run.
/// Ensures that the expected operations, resource type mappings, and operation
/// sequences were persisted correctly for the facility.
/// </summary>
public class NormalizationDatabaseValidator(DualOutputHelper output)
{
    public async Task ValidateAllAsync(string facilityId)
    {
        output.WriteLine("\n");
        output.WriteLine($"\n=== Normalization Database Validation: FacilityId={facilityId} ===\n");

        await using var db = DatabaseConnectionFactory.CreateNormalizationDbContext();

        await ValidateOperations(db, facilityId);
        await ValidateOperationResourceTypes(db, facilityId);
        await ValidateOperationSequences(db, facilityId);

        output.WriteLine("\n=== Normalization Database Validation Complete ===\n");
    }

    private async Task ValidateOperations(NormalizationDbContext db, string facilityId)
    {
        output.WriteLine("[Operation] Validating...");

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

            output.WriteLine($"  Id={op.Id}: Type={op.OperationType}, Name={op.Name}, " +
                             $"ResourceTypes=[{string.Join(", ", resourceTypes)}], " +
                             $"Disabled={op.IsDisabled}");
        }

        output.WriteLine("[Operation] PASS");
    }

    private async Task ValidateOperationResourceTypes(NormalizationDbContext db, string facilityId)
    {
        output.WriteLine("[OperationResourceType] Validating...");

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

                output.WriteLine($"  Operation={op.Id} -> ResourceType={ort.ResourceType.Name} (OrtId={ort.Id})");
            }
        }

        output.WriteLine("[OperationResourceType] PASS");
    }

    private async Task ValidateOperationSequences(NormalizationDbContext db, string facilityId)
    {
        output.WriteLine("[OperationSequence] Validating...");

        var sequences = await PipelineSnapshot.GetOperationSequencesAsync(db, facilityId);

        if (sequences.Count == 0)
        {
            output.WriteLine("  No OperationSequence rows found (sequences are optional)");
            output.WriteLine("[OperationSequence] PASS");
            return;
        }

        foreach (var seq in sequences)
        {
            Assert.NotNull(seq.OperationResourceType);
            Assert.NotNull(seq.OperationResourceType.Operation);
            Assert.NotNull(seq.OperationResourceType.ResourceType);

            var opType = seq.OperationResourceType.Operation.OperationType;
            var resType = seq.OperationResourceType.ResourceType.Name;

            output.WriteLine($"  Id={seq.Id}: Sequence={seq.Sequence}, " +
                             $"OperationType={opType}, ResourceType={resType}");
        }

        // Verify sequences are uniquely ordered (no duplicate sequence numbers)
        var sequenceNumbers = sequences
            .Where(s => s.Sequence.HasValue)
            .Select(s => s.Sequence!.Value)
            .ToList();

        var distinctCount = sequenceNumbers.Distinct().Count();
        Assert.Equal(sequenceNumbers.Count, distinctCount);

        output.WriteLine($"  {sequences.Count} sequence(s) with unique ordering");
        output.WriteLine("[OperationSequence] PASS");
    }
}

