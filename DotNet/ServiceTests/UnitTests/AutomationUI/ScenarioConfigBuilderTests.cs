using Automation.UI.Models;
using Automation.UI.Services;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Models;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class ScenarioConfigBuilderTests
{
    private static ResolvedRunOptions OptionsWith(
        List<ProfiledMeasureType>? measures = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        bool cleanupServiceData = false,
        bool cleanupFhirData = true,
        int polling = 3,
        int maxPolling = 0,
        int loki = 30,
        string nhsnOrganizationId = "10756",
        bool isMetricsRun = false)
    {
        return new ResolvedRunOptions(
            PatientCount: 1,
            ResourcesPerPatient: 1,
            Seed: 1,
            PollingIntervalSeconds: polling,
            MaxPollingDurationMinutes: maxPolling,
            LokiScrapeWindowMinutes: loki,
            CleanupServiceData: cleanupServiceData,
            CleanupFhirData: cleanupFhirData,
            SelectedMeasures: measures ?? [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation],
            PatientProfiles: [],
            PatientCohorts: [])
        {
            ReportPeriodStart = start,
            ReportPeriodEnd = end,
            NhsnOrganizationId = nhsnOrganizationId,
            IsMetricsRun = isMetricsRun,
        };
    }

    [Theory]
    [InlineData(AutomationScenarioKind.AdhocReportTest, "adhoc-report-submission.zip")]
    [InlineData(AutomationScenarioKind.MultiPatientTest, "multi-patient-submission.zip")]
    [InlineData(AutomationScenarioKind.MegaPatientTest, "mega-patient-submission.zip")]
    [InlineData(AutomationScenarioKind.Custom, "custom-submission.zip")]
    public void Download_filename_is_specific_to_each_scenario_kind(
        AutomationScenarioKind scenario, string expected)
    {
        var config = ScenarioConfigBuilder.Build(scenario, OptionsWith());

        config.DownloadFileName.Should().Be(expected);
    }

    [Fact]
    public void Unknown_scenario_kind_throws()
    {
        var act = () => ScenarioConfigBuilder.Build((AutomationScenarioKind)int.MaxValue, OptionsWith());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Measure_bundle_jsons_pass_through()
    {
        var json = """{"resourceType":"Bundle"}""";
        var options = OptionsWith() with { MeasureBundleJsons = [json] };

        var config = ScenarioConfigBuilder.Build(AutomationScenarioKind.Custom, options);

        config.MeasureBundleJsons.Should().ContainSingle().Which.Should().Be(json);
        config.MeasureBundleLocation.Should().BeEmpty();
        config.AdditionalMeasureBundleLocations.Should().BeEmpty();
    }

    [Fact]
    public void Report_period_falls_back_to_2023_window_when_unset()
    {
        var config = ScenarioConfigBuilder.Build(AutomationScenarioKind.Custom, OptionsWith());

        config.StartDate.Should().Be("2023-01-01T00:00:00Z");
        config.EndDate.Should().Be("2023-12-31T23:59:59Z");
    }

    [Fact]
    public void Report_period_uses_resolved_options_when_set()
    {
        var start = new DateTimeOffset(2024, 03, 01, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2024, 03, 31, 23, 59, 59, TimeSpan.Zero);

        var config = ScenarioConfigBuilder.Build(
            AutomationScenarioKind.Custom,
            OptionsWith(start: start, end: end));

        config.StartDate.Should().Be("2024-03-01T00:00:00Z");
        config.EndDate.Should().Be("2024-03-31T23:59:59Z");
    }

    [Fact]
    public void Cleanup_polling_and_loki_window_pass_through_unchanged()
    {
        var config = ScenarioConfigBuilder.Build(
            AutomationScenarioKind.Custom,
            OptionsWith(cleanupServiceData: true, cleanupFhirData: false, polling: 7, maxPolling: 15, loki: 90, nhsnOrganizationId: "22001"));

        config.CleanupServiceData.Should().BeTrue();
        config.CleanupFhirData.Should().BeFalse();
        config.PollingIntervalSeconds.Should().Be(7);
        config.MaxPollingDurationMinutes.Should().Be(15);
        config.LokiScrapeWindowMinutes.Should().Be(90);
        config.NhsnOrganizationId.Should().Be("22001");
        config.PatientIds.Should().BeEmpty();
        config.IsMetricsRun.Should().BeFalse();
    }

    [Fact]
    public void IsMetricsRun_passes_through_to_scenario_config()
    {
        var config = ScenarioConfigBuilder.Build(
            AutomationScenarioKind.Custom,
            OptionsWith(isMetricsRun: true));

        config.IsMetricsRun.Should().BeTrue();
    }
}
