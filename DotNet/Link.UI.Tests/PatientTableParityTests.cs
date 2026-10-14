using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// The report screen's patient table and diagnostic panel moved into the shared manifest.
/// Every column, field, notice and link it had must still be there.
/// </summary>
public class PatientTableParityTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Link.UI")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), "Link.UI", .. parts]));

    [Theory]
    [InlineData(MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, "Mapped", "au-badge-success")]
    [InlineData(MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.PartiallyMapped, "Partial", "au-badge-warning")]
    [InlineData(MappingIndicatorStatus.Unmapped, MappingIndicatorStatus.PartiallyMapped, MappingIndicatorStatus.Mapped, "Not mapped", "au-badge-danger")]
    [InlineData(MappingIndicatorStatus.Mapped, MappingIndicatorStatus.Unknown, MappingIndicatorStatus.Mapped, "Unknown", "au-badge-danger")]
    [InlineData(MappingIndicatorStatus.NotEvaluated, MappingIndicatorStatus.Mapped, MappingIndicatorStatus.NotApplicable, "Not evaluated", "au-badge-muted")]
    [InlineData(MappingIndicatorStatus.NotApplicable, MappingIndicatorStatus.NotApplicable, MappingIndicatorStatus.NotApplicable, "Not applicable", "au-badge-muted")]
    public void Mapping_badge_shows_the_weakest_of_the_three(MappingIndicatorStatus org, MappingIndicatorStatus encounter, MappingIndicatorStatus hsloc, string label, string badge)
    {
        var mapping = ReportPatientDiagnostics.Mapping(org, encounter, hsloc);
        Assert.Equal(label, mapping.Label);
        Assert.Equal(badge, mapping.BadgeClass);
        Assert.Equal(ReportPatientDiagnostics.Words(org), mapping.LocationOrg);
        Assert.Equal(ReportPatientDiagnostics.Words(encounter), mapping.EncounterMapping);
        Assert.Equal(ReportPatientDiagnostics.Words(hsloc), mapping.Hsloc);
        Assert.Contains("Location org: ", mapping.Title);
        Assert.Contains("Encounter mapping: ", mapping.Title);
        Assert.Contains("HSLOC: ", mapping.Title);
    }

    [Fact]
    public void Diagnostics_flag_missing_acquisition_and_normalization()
    {
        var detail = ReportPatientDiagnostics.ToDetail(new ReportEntryDetailApiModel { PatientId = "p-1" });
        Assert.Equal("p-1", detail.PatientId);
        Assert.True(detail.AcquisitionMissing);
        Assert.True(detail.NormalizationMissing);
        Assert.Empty(detail.CodeMaps);
    }

    [Fact]
    public void Every_report_screen_item_has_a_manifest_home()
    {
        var manifest = Read("Views", "Shared", "_ReportManifest.cshtml");
        var diagnostics = Read("Views", "Reports", "_PatientDiagnostics.cshtml");
        var service = Read("Services", "ReportsService.cs");

        // P2-P6: statuses and the three mapping indicators.
        Assert.Contains("<th>Mapping", manifest);
        foreach (var label in new[] { "\"Status\"", "\"Submission\"", "\"Location org\"", "\"Encounter mapping\"", "\"HSLOC\"" })
            Assert.Contains("line(identity, " + label, manifest);
        // The mapping is saved with the manifest and restored when it renders.
        Assert.Contains("LocationOrgStatus = entry.LocationOrgStatus.ToString()", service);
        Assert.Contains("ReportPatientDiagnostics.Mapping(location, encounter, hsloc)", service);

        // P7-P8: Measure and Log row actions with the go-to-page icon.
        Assert.Contains("title=\"Measure\"", manifest);
        Assert.Contains("title=\"Log\"", manifest);
        Assert.Contains("\"/Reports/Acquisition\"", service);

        // D1-D9: diagnostics panel content, loaded only when the modal opens.
        foreach (var text in new[]
                 {
                     "Data acquisition has not reported for this patient.",
                     "Encounters", "In the organization", "Assumed in the organization", "Locations",
                     "Normalization has not reported code maps for this patient.",
                     "No code map totals."
                 })
            Assert.Contains(text, diagnostics);
        Assert.Contains("That patient is not on this report.", Read("Services", "ReportPatientDiagnostics.cs"));
        Assert.Contains("loadDiagnostics(openRow);", manifest);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(manifest, @"fetch\(row\.diagnostics"));
    }

    [Fact]
    public void Report_screen_links_to_the_manifest_instead_of_duplicating_it()
    {
        var report = Read("Views", "Tenants", "Report.cshtml");
        Assert.DoesNotContain("<table", report);
        var body = Read("Views", "Tenants", "_ReportBody.cshtml");
        Assert.DoesNotContain("manifest-patients", body);
        Assert.Contains("id=\"report-patients-link\"", body);
        Assert.Contains("tab = \"patients\"", body);

        var view = Read("Services", "FacilityViewService.cs");
        Assert.DoesNotContain("LoadPatientAsync", view);
        Assert.DoesNotContain("ToPatient", view);

        // R1: old ?patientId= / ?patient= links land on the manifest Patients tab.
        Assert.Contains("q = deepLink", Read("Controllers", "TenantsController.cs"));
    }

    [Fact]
    public void Automation_quick_launch_sits_above_the_runs_table()
    {
        var index = Read("Views", "Automation", "Index.cshtml");
        Assert.True(index.IndexOf("id=\"quickLaunchCard\"", StringComparison.Ordinal) < index.IndexOf("id=\"recentRunsHost\"", StringComparison.Ordinal));
    }
}
