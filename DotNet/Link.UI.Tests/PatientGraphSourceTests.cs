using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class PatientGraphSourceTests
{
    [Fact]
    public void Local_settings_read_the_submitted_patient_file_from_the_external_container()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoFile("DotNet/Link.UI/appsettings.Development.json")));
        var read = doc.RootElement.GetProperty("SubmissionRead");
        read.GetProperty("ExternalConnectionString").GetString().Should().NotBeNullOrWhiteSpace();
        read.GetProperty("ExternalContainerName").GetString().Should().Be("external");
    }

    [Fact]
    public void Graph_totals_come_only_from_the_stored_report_counts()
    {
        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/resource-graph.js"));
        script.Should().Contain("var stored = storedCounts(row);");
        script.Should().Contain("row.resources");
        script.Should().NotContain("done.types");
        script.Should().NotContain("done.source");
        script.Should().NotContain("not the submitted bundle");
        script.Should().Contain("No resource counts were recorded.");
    }

    [Fact]
    public void Patient_graph_has_no_measure_eval_fallback()
    {
        var controller = File.ReadAllText(RepoFile("DotNet/Link.UI/Controllers/ReportsController.cs"));
        var start = controller.IndexOf("public async Task<IActionResult> PatientGraph(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var end = controller.IndexOf("private async Task<(bool Handled", start, StringComparison.Ordinal);
        var action = controller[start..end];
        action.Should().NotContain("ReadPatientBundleAsync");
        controller.Should().NotContain("LoadGraphAsync");
        action.Should().Contain("The submitted file for this patient is not available.");

        var rules = File.ReadAllText(RepoFile("DotNet/Link.UI/Services/ResourceGraphRules.cs"));
        rules.Should().NotContain("ReadBundle(");
        rules.Should().NotContain("= \"bundle\"");
    }
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DotNet", "Link.UI", "Link.UI.csproj")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}