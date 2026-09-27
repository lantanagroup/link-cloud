using System.Text.Json;
using Automation.UI.Controllers;
using Automation.UI.Models;
using Automation.UI.Services;
using Automation.UI.Services.Persistence;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class LeftoverRunCleanupDefaultsTests
{
    [Fact]
    public void CodeDefaults_LeaveScheduledCleanupOff()
    {
        var options = new LeftoverRunCleanupOptions();
        AssertSwitchesOff(options.Enabled, options.QuiesceEnabled, options.DailyTeardownEnabled, options.WeeklyHistoryPurgeEnabled);
        options.DailyTeardownTimeUtc.Should().Be("10:00");
        options.WeeklyHistoryPurgeDay.Should().Be(DayOfWeek.Sunday);
        options.WeeklyHistoryPurgeTimeUtc.Should().Be("10:00");
        options.TeardownRetention.Should().Be(TimeSpan.FromDays(14));

        var settings = new LeftoverRunCleanupSettings();
        AssertSwitchesOff(settings.Enabled, settings.QuiesceEnabled, settings.DailyTeardownEnabled, settings.WeeklyHistoryPurgeEnabled);
        settings.DailyTeardownTimeUtc.Should().Be(new TimeOnly(10, 0));
        settings.WeeklyHistoryPurgeDay.Should().Be(DayOfWeek.Sunday);
        settings.TeardownRetention.Should().Be(TimeSpan.FromDays(14));
    }

    [Fact]
    public void ApplyForm_LeavesScheduleSwitchesOffWhenTheFormDoesNotEnableThem()
    {
        var settings = CleanupController.ApplyForm(new CleanupSettingsForm(), new LeftoverRunCleanupSettings());

        AssertSwitchesOff(settings.Enabled, settings.QuiesceEnabled, settings.DailyTeardownEnabled, settings.WeeklyHistoryPurgeEnabled);
        settings.DailyTeardownTimeUtc.Should().Be(new TimeOnly(10, 0));
        settings.WeeklyHistoryPurgeTimeUtc.Should().Be(new TimeOnly(10, 0));
        settings.TeardownRetention.Should().Be(TimeSpan.FromDays(14));
    }

    [Theory]
    [InlineData("DotNet/Automation.UI/appsettings.json")]
    [InlineData("DotNet/Automation.UI/appsettings.Docker.json")]
    public void ShippedConfiguration_LeavesScheduledCleanupOff(string relativePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(RepoFile(relativePath)));
        var section = document.RootElement.GetProperty("LeftoverRunCleanup");

        AssertSwitchesOff(
            section.GetProperty("Enabled").GetBoolean(),
            section.GetProperty("QuiesceEnabled").GetBoolean(),
            section.GetProperty("DailyTeardownEnabled").GetBoolean(),
            section.GetProperty("WeeklyHistoryPurgeEnabled").GetBoolean());
        section.GetProperty("DailyTeardownTimeUtc").GetString().Should().Be("10:00");
        section.GetProperty("WeeklyHistoryPurgeDay").GetString().Should().Be("Sunday");
        section.GetProperty("WeeklyHistoryPurgeTimeUtc").GetString().Should().Be("10:00");
    }

    [Fact]
    public void DockerCompose_DoesNotTurnCleanupOnWhenTheVariableIsUnset()
    {
        var compose = File.ReadAllText(RepoFile("docker-compose.yml"));
        compose.Should().Contain("LeftoverRunCleanup__Enabled: ${LEFTOVER_CLEANUP_ENABLED:-false}");
        compose.Should().NotContain("LEFTOVER_CLEANUP_ENABLED:-true");
    }

    [Fact]
    public void AppConfigCatalog_RecordsCleanupSwitchesOff()
    {
        var catalog = File.ReadAllText(RepoFile("app-config.yaml"));
        foreach (var key in new[]
        {
            "LeftoverRunCleanup:Enabled",
            "LeftoverRunCleanup:QuiesceEnabled",
            "LeftoverRunCleanup:DailyTeardownEnabled",
            "LeftoverRunCleanup:WeeklyHistoryPurgeEnabled"
        })
        {
            var keyIndex = catalog.IndexOf($"key: \"{key}\"", StringComparison.Ordinal);
            keyIndex.Should().BeGreaterThan(-1, $"catalog should list {key}");
            var nextKey = catalog.IndexOf("\n    - key:", keyIndex + 1, StringComparison.Ordinal);
            var end = nextKey < 0 ? catalog.Length : nextKey;
            var window = catalog[keyIndex..end];
            window.Should().Contain("defaultValue: \"false\"");
            window.Should().Contain("required: false");
        }
    }

    private static void AssertSwitchesOff(bool enabled, bool quiesce, bool dailyTeardown, bool weeklyPurge)
    {
        enabled.Should().BeFalse();
        quiesce.Should().BeFalse();
        dailyTeardown.Should().BeFalse();
        weeklyPurge.Should().BeFalse();
    }

    private static string RepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not find {relativePath} by walking up from {AppContext.BaseDirectory}.");
    }
}
