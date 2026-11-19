using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Shared.Application.Models.Retention;

namespace UnitTests.Report;

/// <summary>
/// Covers how <see cref="RetentionWindowParser"/> reads an ISO-8601 retention window into months or days.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionWindowParserTests
{
    [Theory]
    [InlineData("P3M", RetentionWindowUnit.Months, 3)]
    [InlineData("P1Y", RetentionWindowUnit.Months, 12)]
    [InlineData("P30D", RetentionWindowUnit.Days, 30)]
    [InlineData("P2W", RetentionWindowUnit.Days, 14)]
    [InlineData(" p3m ", RetentionWindowUnit.Months, 3)]
    public void TryParse_ValidValue_ReturnsWindow(string text, RetentionWindowUnit unit, int count)
    {
        var parsed = RetentionWindowParser.TryParse(text, out var window, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.Equal(unit, window!.Unit);
        Assert.Equal(count, window.Count);
    }

    [Theory]
    [InlineData("P36M", RetentionWindowUnit.Months, 36)]
    [InlineData("P3Y", RetentionWindowUnit.Months, 36)]
    [InlineData("P1095D", RetentionWindowUnit.Days, 1095)]
    [InlineData("P156W", RetentionWindowUnit.Days, 1092)]
    public void TryParse_AtTheLimit_ReturnsWindow(string text, RetentionWindowUnit unit, int count)
    {
        var parsed = RetentionWindowParser.TryParse(text, out var window, out _);

        Assert.True(parsed);
        Assert.Equal(unit, window!.Unit);
        Assert.Equal(count, window.Count);
    }

    [Theory]
    [InlineData("P37M", "37 months")]
    [InlineData("P4Y", "48 months")]
    [InlineData("P1096D", "1096 days")]
    [InlineData("P157W", "1099 days")]
    [InlineData("P999999999Y", "11999999988 months")]
    public void TryParse_OverTheLimit_ReturnsErrorWithConvertedLength(string text, string length)
    {
        var parsed = RetentionWindowParser.TryParse(text, out var window, out var error);

        Assert.False(parsed);
        Assert.Null(window);
        Assert.Contains(length, error);
    }

    [Theory]
    [InlineData("P0M")]
    [InlineData("P0D")]
    public void TryParse_Zero_ReturnsError(string text)
    {
        var parsed = RetentionWindowParser.TryParse(text, out var window, out var error);

        Assert.False(parsed);
        Assert.Null(window);
        Assert.Contains("is zero", error);
    }

    [Theory]
    [InlineData("P1M15D")]
    [InlineData("P1Y6M")]
    [InlineData("PT24H")]
    [InlineData("P1DT2H")]
    [InlineData("3M")]
    [InlineData("P3")]
    [InlineData("P-3M")]
    [InlineData("P1.5M")]
    [InlineData("P1234567890D")]
    [InlineData("P٣M")]
    [InlineData("90")]
    public void TryParse_NotASingleComponent_ReturnsError(string text)
    {
        var parsed = RetentionWindowParser.TryParse(text, out var window, out var error);

        Assert.False(parsed);
        Assert.Null(window);
        Assert.Contains("is not a single ISO-8601", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_Empty_ReturnsError(string? text)
    {
        var parsed = RetentionWindowParser.TryParse(text, out var window, out var error);

        Assert.False(parsed);
        Assert.Null(window);
        Assert.Contains("is empty", error);
    }
}
