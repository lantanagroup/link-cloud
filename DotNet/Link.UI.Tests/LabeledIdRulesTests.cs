using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class LabeledIdRulesTests
{
    [Theory]
    [InlineData(null, "abc", null)]
    [InlineData("  ", "abc", null)]
    [InlineData("abc", "abc", null)]
    [InlineData("ABC", "abc", null)]
    [InlineData(" North ", "abc", "North")]
    public void Display_name_is_omitted_when_it_repeats_the_id(string? name, string? value, string? expected)
    {
        LabeledIdRules.DisplayName(name, value).Should().Be(expected);
    }
}
