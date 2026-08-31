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

    [Fact]
    public void Split_EmptyParameters_ReturnsOneEmptyBatch()
    {
        var batches = FhirSearchLimits.SplitOversizedIdParameters([]);

        Assert.Single(batches);
        Assert.Empty(batches[0]);
    }
}
