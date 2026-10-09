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

    [Theory]
    [InlineData("6c5466db-d467-46a1-b89e-2a9adc72b0f7", true)]
    [InlineData("6C5466DBD46746A1B89E2A9ADC72B0F7", true)]
    [InlineData("ReadyToAcquire", false)]
    [InlineData("1", false)]
    [InlineData("measureeval", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Copy_is_offered_only_for_a_guid(string? value, bool expected)
    {
        LabeledIdRules.IsGuid(value).Should().Be(expected);
    }
}
