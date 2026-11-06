using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Report;

/// <summary>
/// Covers how <see cref="RetentionCheckSettings"/> reads and validates Retention:Window and
/// Retention:MaxReportsPerCleanup.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCheckSettingsTests
{
    [Fact]
    public void FromConfiguration_KeysNotSet_ReturnsThreeMonthsAndTwentyFive()
    {
        var settings = Read(new Dictionary<string, string?>());

        Assert.True(settings.IsValid);
        Assert.Equal("P3M", settings.WindowText);
        Assert.Equal(RetentionWindowUnit.Months, settings.Window!.Unit);
        Assert.Equal(3, settings.Window.Count);
        Assert.Equal(25, settings.MaxReportsPerCleanup);
    }

    [Fact]
    public void FromConfiguration_ValidValues_ReturnsThem()
    {
        var settings = Read(new Dictionary<string, string?>
        {
            ["Retention:Window"] = "P2W",
            ["Retention:MaxReportsPerCleanup"] = " 50 "
        });

        Assert.True(settings.IsValid);
        Assert.Equal("P2W", settings.WindowText);
        Assert.Equal(RetentionWindowUnit.Days, settings.Window!.Unit);
        Assert.Equal(14, settings.Window.Count);
        Assert.Equal(50, settings.MaxReportsPerCleanup);
    }

    [Fact]
    public void FromConfiguration_InvalidWindow_IsInvalidWithoutFallingBack()
    {
        var settings = Read(new Dictionary<string, string?>
        {
            ["Retention:Window"] = "P90"
        });

        Assert.False(settings.IsValid);
        Assert.Null(settings.Window);
        Assert.Equal("P90", settings.WindowText);
        Assert.StartsWith("Retention:Window 'P90' is not", Assert.Single(settings.Errors));
    }

    [Fact]
    public void FromConfiguration_EmptyWindow_IsInvalid()
    {
        var settings = Read(new Dictionary<string, string?>
        {
            ["Retention:Window"] = ""
        });

        Assert.False(settings.IsValid);
        Assert.Equal("Retention:Window is empty. Use an ISO-8601 duration such as P3M.",
                     Assert.Single(settings.Errors));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("ten")]
    [InlineData("2.5")]
    [InlineData("")]
    [InlineData("99999999999")]
    public void FromConfiguration_InvalidCap_IsInvalid(string value)
    {
        var settings = Read(new Dictionary<string, string?>
        {
            ["Retention:MaxReportsPerCleanup"] = value
        });

        Assert.False(settings.IsValid);
        Assert.Equal(0, settings.MaxReportsPerCleanup);
        Assert.StartsWith("Retention:MaxReportsPerCleanup", Assert.Single(settings.Errors));
    }

    [Fact]
    public void FromConfiguration_BothInvalid_ReportsBothErrors()
    {
        var settings = Read(new Dictionary<string, string?>
        {
            ["Retention:Window"] = "P37M",
            ["Retention:MaxReportsPerCleanup"] = "0"
        });

        Assert.False(settings.IsValid);
        Assert.Equal(2, settings.Errors.Count);
        Assert.StartsWith("Retention:Window", settings.Errors[0]);
        Assert.StartsWith("Retention:MaxReportsPerCleanup", settings.Errors[1]);
    }

    [Fact]
    public void FromConfiguration_JsonValues_ReadsThem()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(
                """{ "Retention": { "Window": "P1Y", "MaxReportsPerCleanup": 10 } }"""u8.ToArray()))
            .Build();

        var settings = RetentionCheckSettings.FromConfiguration(configuration);

        Assert.True(settings.IsValid);
        Assert.Equal(12, settings.Window!.Count);
        Assert.Equal(10, settings.MaxReportsPerCleanup);
    }

    private static RetentionCheckSettings Read(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return RetentionCheckSettings.FromConfiguration(configuration);
    }
}
