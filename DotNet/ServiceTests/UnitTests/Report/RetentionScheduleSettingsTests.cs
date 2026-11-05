using LantanaGroup.Link.Report.Application.Options;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Report;

/// <summary>
/// Covers how the retention schedule settings bind from configuration and which cron expression the
/// reconciler is given when the configured one is missing or cannot be parsed.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionScheduleSettingsTests
{
    [Fact]
    public void Constructor_NothingConfigured_DefaultsToDailyAtTwoUtcAndEnabled()
    {
        var settings = new RetentionScheduleSettings();

        Assert.Equal("0 0 2 * * ?", settings.Cron);
        Assert.True(settings.Enabled);
        Assert.True(settings.CronIsValid);
        Assert.Equal(RetentionScheduleSettings.DefaultCron, settings.ResolvedCron);
    }

    [Theory]
    [InlineData("0 30 3 * * ?")]
    [InlineData("0 0 2 ? * MON")]
    [InlineData("0 0 2 * * ? 2099")]
    public void ResolvedCron_ValidCron_ReturnsConfigured(string cron)
    {
        var settings = new RetentionScheduleSettings { Cron = cron };

        Assert.True(settings.CronIsValid);
        Assert.Equal(cron, settings.ResolvedCron);
    }

    [Theory]
    [InlineData("every day at 2")]
    [InlineData("0 0 2 * *")]
    [InlineData("0 0 25 * * ?")]
    [InlineData("0 0 2 * * MON")]
    [InlineData("0 0 2 1 1 ? 2020")]
    public void ResolvedCron_InvalidCron_ReturnsDefault(string cron)
    {
        var settings = new RetentionScheduleSettings { Cron = cron };

        Assert.False(settings.CronIsValid);
        Assert.Equal(RetentionScheduleSettings.DefaultCron, settings.ResolvedCron);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ResolvedCron_BlankCron_ReturnsDefault(string? cron)
    {
        var settings = new RetentionScheduleSettings { Cron = cron! };

        Assert.False(settings.CronIsValid);
        Assert.Equal(RetentionScheduleSettings.DefaultCron, settings.ResolvedCron);
    }

    [Fact]
    public void Bind_SectionConfigured_ReadsCronAndEnabled()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Retention:Schedule:Cron"] = "0 15 4 * * ?",
            ["Retention:Schedule:Enabled"] = "false"
        });

        var settings = Bind(configuration);

        Assert.Equal("0 15 4 * * ?", settings.Cron);
        Assert.False(settings.Enabled);
        Assert.Equal("0 15 4 * * ?", settings.ResolvedCron);
    }

    [Fact]
    public void Bind_SectionMissing_KeepsDefaults()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["PatientAggregator:IncludeOrganizationResource"] = "true"
        });

        var settings = Bind(configuration);

        Assert.Equal(RetentionScheduleSettings.DefaultCron, settings.Cron);
        Assert.True(settings.Enabled);
    }

    [Fact]
    public void Bind_EmptyCronValue_FallsBackToDefault()
    {
        // An App Configuration row with no value binds as an empty string, not as a missing key.
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Retention:Schedule:Cron"] = ""
        });

        var settings = Bind(configuration);

        Assert.False(settings.CronIsValid);
        Assert.Equal(RetentionScheduleSettings.DefaultCron, settings.ResolvedCron);
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private static RetentionScheduleSettings Bind(IConfiguration configuration)
    {
        var settings = new RetentionScheduleSettings();
        configuration
            .GetSection(RetentionScheduleSettings.Key)
            .Bind(settings);

        return settings;
    }
}
