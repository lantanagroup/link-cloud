using System.Text.Json;
using FluentAssertions;
using LantanaGroup.Automation.Generation;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class ReportManifestArchiveTests
{
    [Fact]
    public void The_key_for_a_report_is_stable_and_differs_between_reports()
    {
        var report = Guid.Parse("08ea535d-084d-41f9-90cc-6d9c730ae5ec");
        var other = Guid.Parse("4a79b81a-9a65-44de-85ca-4eac3192f00a");

        ReportManifestArchive.KeyFor(report).Should().Be(ReportManifestArchive.KeyFor(report));
        ReportManifestArchive.KeyFor(report).Should().NotBe(ReportManifestArchive.KeyFor(other));
        ReportManifestArchive.KeyFor(report).Should().NotBe(report);
        ReportManifestArchive.KeyFor(report).ToString("D")[14].Should().Be('5');
    }

    [Fact]
    public void A_saved_report_section_round_trips_inside_the_generation_manifest_model()
    {
        var snapshot = new GenerationManifestSnapshot
        {
            PatientCount = 1,
            Report = new ReportManifestSection
            {
                ReportId = Guid.NewGuid().ToString(),
                FacilityId = "fac-1",
                FacilityName = "Facility one",
                RunId = Guid.NewGuid(),
                BuiltAt = DateTimeOffset.UtcNow,
                PatientCount = 1,
                Populations = [new ReportManifestPopulation { Measure = "ACH", PopulationId = "initial-population", Count = 1 }],
                Patients =
                [
                    new ReportManifestPatient
                    {
                        PatientId = "p1",
                        ReportingStatus = "Submitted",
                        ResourceCounts = new(StringComparer.OrdinalIgnoreCase) { ["Encounter"] = 2 },
                        MeasureReportIds = ["mr-1"],
                        Populations = ["ACH|initial-population"],
                        Timeline = [new ReportManifestTimelineEntry { Label = "Identified", At = new DateTime(2026, 10, 1) }]
                    }
                ]
            }
        };

        var back = JsonSerializer.Deserialize<GenerationManifestSnapshot>(JsonSerializer.Serialize(snapshot))!;

        back.Report.Should().NotBeNull();
        back.Report!.RunId.Should().Be(snapshot.Report.RunId);
        back.Report.Patients.Single().Populations.Should().Equal("ACH|initial-population");
        back.Report.Patients.Single().ResourceCounts["Encounter"].Should().Be(2);
        back.Report.Patients.Single().Timeline.Single().Label.Should().Be("Identified");
    }

    [Fact]
    public void Run_keyed_generation_manifests_without_a_report_section_still_read()
    {
        var back = JsonSerializer.Deserialize<GenerationManifestSnapshot>("{\"PatientCount\":3}")!;
        back.PatientCount.Should().Be(3);
        back.Report.Should().BeNull();
    }

    [Fact]
    public void Only_a_finished_report_is_saved_and_automation_saves_before_cleanup()
    {
        var service = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ReportsService.cs"));
        service.Should().Contain("if (opened.Schedule!.EndOfReportPeriodJobHasRun)");
        service.Should().NotContain("BundleJson");

        var executor = File.ReadAllText(RepoFile("DotNet/Link.UI/Engine/Services/RunExecutor.cs"));
        var save = executor.IndexOf("await SaveReportManifestAsync(", StringComparison.Ordinal);
        var cleanup = executor.IndexOf("await RunCleanupHelper.CleanupAfterRunAsync(", StringComparison.Ordinal);
        save.Should().BeGreaterThan(0);
        save.Should().BeLessThan(cleanup);
    }

    [Fact]
    public void Interactive_mode_is_disabled_with_a_tooltip_when_the_submission_is_gone()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_ReportManifest.cshtml"));
        view.Should().Contain("id=\"manifest-interactive-wrap\"");
        view.Should().Contain("data-bs-toggle=\"tooltip\" title=\"@(Model.InteractiveBlocked");
        view.Should().Contain("disabled=\"@(Model.InteractiveBlocked is not null)\"");

        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/resource-graph.js"));
        script.Should().Contain("if (error.status === 404)");
        script.Should().Contain("function unavailable(text)");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}