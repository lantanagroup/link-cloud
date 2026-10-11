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
    public void Graph_marks_counts_that_come_from_acquired_data_instead_of_the_submitted_bundle()
    {
        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/resource-graph.js"));
        script.Should().Contain("source: done.source");
        script.Should().Contain("state.source === \"bundle\"");
        script.Should().Contain("not the submitted bundle");
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