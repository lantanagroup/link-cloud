using Automation.UI.Models;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;

namespace Automation.UI.Services;

/// <summary>
/// Builds the <see cref="TestScenarioConfig"/> handed to the run pipeline. Pure
/// translation of <see cref="ResolvedRunOptions"/> + <see cref="AutomationScenarioKind"/>
/// into the legacy config shape the Link pipeline already understands.
/// </summary>
public static class ScenarioConfigBuilder
{
    public static TestScenarioConfig Build(AutomationScenarioKind scenario, ResolvedRunOptions options)
    {
        var downloadFileName = scenario switch
        {
            AutomationScenarioKind.AdhocReportTest => "adhoc-report-submission.zip",
            AutomationScenarioKind.MultiPatientTest => "multi-patient-submission.zip",
            AutomationScenarioKind.MegaPatientTest => "mega-patient-submission.zip",
            AutomationScenarioKind.Custom => "custom-submission.zip",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        var inlineJsons = options.MeasureBundleJsons
            .Where(j => !string.IsNullOrWhiteSpace(j))
            .ToList();
        var bundleLocations = inlineJsons.Count > 0
            ? []
            : options.SelectedMeasures.Select(ProfiledMeasureCatalog.GetBundleLocation).ToList();

        return new TestScenarioConfig
        {
            MeasureBundleLocation = bundleLocations.Count > 0 ? bundleLocations[0] : "",
            AdditionalMeasureBundleLocations = bundleLocations.Count > 1 ? bundleLocations.Skip(1).ToList() : [],
            MeasureBundleJsons = inlineJsons,
            StartDate = options.ReportPeriodStart.HasValue
                ? options.ReportPeriodStart.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                : "2023-01-01T00:00:00Z",
            EndDate = options.ReportPeriodEnd.HasValue
                ? options.ReportPeriodEnd.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                : "2023-12-31T23:59:59Z",
            NhsnOrganizationId = options.NhsnOrganizationId,
            PatientIds = [],
            CleanupServiceData = options.CleanupServiceData,
            CleanupFhirData = options.CleanupFhirData,
            PollingIntervalSeconds = options.PollingIntervalSeconds,
            MaxPollingDurationMinutes = options.MaxPollingDurationMinutes,
            DownloadFileName = downloadFileName,
            LokiScrapeWindowMinutes = options.LokiScrapeWindowMinutes,
            IsMetricsRun = options.IsMetricsRun
        };
    }
}
