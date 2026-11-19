using LantanaGroup.Link.DataAcquisition.Domain.Extensions;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class ResourceIdExtensionsTests
{
    [Theory]
    [InlineData("Observation/123", "Observation")]
    [InlineData("Observation/123/_history/2", "Observation")]
    [InlineData("bare-id", "bare-id")]
    [InlineData("/leading-slash", "/leading-slash")]
    [InlineData("Patient/", "Patient")]
    public void ToResourceTypeKey_IdShapes_ReturnsTextBeforeFirstSlash(string resourceId, string expected)
    {
        Assert.Equal(expected, resourceId.ToResourceTypeKey());
    }

    [Fact]
    public void ToResourceTypeCounts_MixedIds_CountsPerTypeAndSkipsEmpty()
    {
        string[] resourceIds = ["Observation/1", "Observation/2", "Condition/c1", "", "bare-id"];

        var counts = resourceIds
            .ToResourceTypeCounts(42)
            .ToDictionary(c => c.ResourceType, c => c.Count);

        var expected = new Dictionary<string, int>
        {
            ["Observation"] = 2,
            ["Condition"] = 1,
            ["bare-id"] = 1
        };
        Assert.Equal(expected, counts);
    }

    [Fact]
    public void ToResourceTypeCounts_TypesDifferingOnlyInCase_GroupsThemTogether()
    {
        string[] resourceIds = ["Observation/1", "observation/2"];

        var count = Assert.Single(resourceIds.ToResourceTypeCounts(42));

        Assert.Equal("Observation", count.ResourceType);
        Assert.Equal(2, count.Count);
        Assert.Equal(42, count.DataAcquisitionLogId);
    }

    [Fact]
    public void ToResourceTypeCounts_NoIds_ReturnsNoRows()
    {
        Assert.Empty(Array.Empty<string>().ToResourceTypeCounts(42));
    }
}
