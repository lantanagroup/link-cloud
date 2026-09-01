using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class FhirSearchParameterBatcherTests
{
    [Fact]
    public void Split_NoOversizedParameter_ReturnsOriginalList()
    {
        var parameters = new List<string>
        {
            "patient=Patient-1",
            "encounter=enc-1,enc-2"
        };

        var batches = FhirSearchLimits.SplitOversizedIdParameters(parameters, maxIds: 20);

        Assert.Single(batches);
        Assert.Equal(parameters, batches[0]);
    }

    [Fact]
    public void Split_OversizedEncounterList_ChunksAndKeepsPatient()
    {
        var encounterIds = Enumerable.Range(1, 45).Select(i => $"enc-{i}").ToList();
        var parameters = new List<string>
        {
            "patient=Patient-1",
            $"encounter={string.Join(',', encounterIds)}"
        };

        var batches = FhirSearchLimits.SplitOversizedIdParameters(parameters, maxIds: 20);

        Assert.Equal(3, batches.Count);
        Assert.All(batches, batch => Assert.Contains("patient=Patient-1", batch));
        Assert.Equal(20, batches[0].Single(p => p.StartsWith("encounter=")).Split('=')[1].Split(',').Length);
        Assert.Equal(20, batches[1].Single(p => p.StartsWith("encounter=")).Split('=')[1].Split(',').Length);
        Assert.Equal(5, batches[2].Single(p => p.StartsWith("encounter=")).Split('=')[1].Split(',').Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Split_NonPositiveMaxIds_UsesDefaultCap(int maxIds)
    {
        var encounterIds = Enumerable.Range(1, 150).Select(i => $"enc-{i}");
        var parameters = new List<string>
        {
            "patient=Patient-1",
            $"encounter={string.Join(',', encounterIds)}"
        };

        var batches = FhirSearchLimits.SplitOversizedIdParameters(parameters, maxIds);

        Assert.Equal(2, batches.Count);
        Assert.All(batches, batch =>
            Assert.True(batch.Single(p => p.StartsWith("encounter=")).Split('=')[1].Split(',').Length
                        <= FhirSearchLimits.MaxIdsPerParameter));
    }

    [Fact]
    public void Split_TwoOversizedParameters_CartesianChunks()
    {
        var encounterIds = string.Join(',', Enumerable.Range(1, 3).Select(i => $"enc-{i}"));
        var resourceIds = string.Join(',', Enumerable.Range(1, 3).Select(i => $"id-{i}"));
        var parameters = new List<string>
        {
            "patient=Patient-1",
            $"encounter={encounterIds}",
            $"_id={resourceIds}"
        };

        var batches = FhirSearchLimits.SplitOversizedIdParameters(parameters, maxIds: 2);

        Assert.Equal(4, batches.Count);
        Assert.All(batches, batch =>
        {
            Assert.Contains("patient=Patient-1", batch);
            Assert.True(batch.Single(p => p.StartsWith("encounter=")).Split('=')[1].Split(',').Length <= 2);
            Assert.True(batch.Single(p => p.StartsWith("_id=")).Split('=')[1].Split(',').Length <= 2);
        });
    }

    [Fact]
    public void Split_EmptyParameters_ReturnsOneEmptyBatch()
    {
        var batches = FhirSearchLimits.SplitOversizedIdParameters([]);

        Assert.Single(batches);
        Assert.Empty(batches[0]);
    }
}
