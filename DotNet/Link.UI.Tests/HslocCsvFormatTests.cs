using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class HslocCsvFormatTests
{
    [Fact]
    public void The_replace_form_explains_the_required_csv_format()
    {
        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Configuration/Hsloc.cshtml"));
        var script = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/js/hsloc-csv.js"));

        view.Should().Contain("id=\"hsloc-format-help\"");
        view.Should().Contain("role=\"note\"");
        view.Should().Contain("aria-label=\"Required HSLOC CSV format\"");
        view.Should().Contain("<strong>The HSLOC CSV published on the CDC website cannot be uploaded as-is.</strong>");
        view.Should().Contain("Reformat it so the first row is a header containing exactly these four columns, in any order:");
        view.Should().Contain("<code>CDCCode</code>: NHSN CDC location code, for example IN:ACUTE:CC:M");
        view.Should().Contain("<code>ShortDescription</code>: Short location description");
        view.Should().Contain("<code>HSLOCCode</code>: HL7 HSLOC code, for example 1027-2");
        view.Should().Contain("<code>LongDescription</code>: Full location description");
        view.Should().Contain("Every row needs a value in all four columns, each HSLOC code may appear only once, and any other columns must be removed.");
        view.Should().Contain("CDCCode,ShortDescription,HSLOCCode,LongDescription");
        view.Should().Contain("IN:ACUTE:CC:M,Medical Critical Care,1027-2,Medical Critical Care");
        view.Should().Contain("aria-describedby=\"hsloc-format-help file-error\"");
        view.Should().Contain("id=\"file-error\"");
        view.Should().Contain("role=\"alert\"");
        view.Should().Contain("id=\"hsloc-replace\"");
        view.Should().Contain("asp-action=\"ReplaceHsloc\"");
        view.Should().Contain("data-hold=");
        view.Should().Contain("src=\"~/js/hsloc-csv.js\"");

        script.Should().Contain("Select a non-empty CSV file.");
        script.Should().Contain("Unable to read the selected file.");
        script.Should().Contain("This file is not in the required HSLOC format.");
        script.Should().Contain("64 * 1024");
        script.Should().Contain("button.disabled");
        script.Should().Contain("input.value = ''");
        script.Should().Contain("getElementById('hsloc-replace')");
        script.Should().Contain("getElementById('file-error')");
    }

    [Fact]
    public async Task Header_helper_matches_the_admin_upload_rules()
    {
        var test = RepoFile("DotNet/Link.UI.Tests/hsloc-csv.test.js");
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("--test");
        start.ArgumentList.Add(test);

        using var process = Process.Start(start);
        process.Should().NotBeNull();
        var stdoutTask = process!.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var exited = process.WaitForExit(30000);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        exited.Should().BeTrue("node --test did not finish");
        process.ExitCode.Should().Be(0, stdout + Environment.NewLine + stderr);
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "link-cloud.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return Path.Combine(dir!.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
