using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class EmptyCardTests
{
    [Fact]
    public void The_patient_modal_hides_timeline_and_related_when_they_are_empty()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Shared/_ReportManifest.cshtml"));
        view.Should().Contain("id=\"manifest-patient-events-card\" hidden");
        view.Should().Contain("id=\"manifest-patient-links-card\" hidden");
        view.Should().Contain("manifest-patient-events-card\").hidden = ");
        view.Should().Contain("manifest-patient-links-card\").hidden = ");
        view.Should().NotContain("No acquisition or normalization time was recorded.");
        view.Should().NotContain("No measure page or log link is available for this patient.");
    }

    [Fact]
    public void The_dashboard_runs_card_shows_counts_without_placeholder_text()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Home/_Overview.cshtml"));
        view.Should().NotContain("No runs yet.");
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
