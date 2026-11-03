using FluentAssertions;
using LantanaGroup.Link.Automation.Link.Helpers;

namespace UnitTests.AutomationLink;

[Trait("Category", "UnitTests")]
public class AcquisitionTerminalFailuresTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(0, 2)]
    [InlineData(3, 2)]
    public void Count_EachCombination_ReturnsBothCounts(int maxRetries, int accessDenied)
    {
        var statusCounts = new List<PipelineDataReader.StatusCountInfo>
        {
            new("Completed", 40),
            new("Failed", 1)
        };

        if (maxRetries > 0)
        {
            statusCounts.Add(new("MaxRetriesReached", maxRetries));
        }

        if (accessDenied > 0)
        {
            statusCounts.Add(new("AccessDenied", accessDenied));
        }

        var counts = AcquisitionTerminalFailures.Count(statusCounts);

        counts.MaxRetriesReached.Should().Be(maxRetries);
        counts.AccessDenied.Should().Be(accessDenied);
    }

    [Fact]
    public void Count_StatusNamesInAnotherCase_StillCounts()
    {
        var counts = AcquisitionTerminalFailures.Count(
        [
            new("maxretriesreached", 1),
            new("ACCESSDENIED", 4)
        ]);

        counts.Should().Be((1, 4));
    }

    [Fact]
    public void Count_NoBreakdown_ReturnsZeros()
    {
        AcquisitionTerminalFailures.Count(null).Should().Be((0, 0));
    }

    [Theory]
    [InlineData("MaxRetriesReached", true)]
    [InlineData("AccessDenied", true)]
    [InlineData("accessdenied", true)]
    [InlineData("Failed", false)]
    [InlineData("Completed", false)]
    [InlineData("ConfigurationMissing", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTerminalFailure_Status_ReturnsExpected(string? status, bool expected)
    {
        AcquisitionTerminalFailures.IsTerminalFailure(status).Should().Be(expected);
    }
}
