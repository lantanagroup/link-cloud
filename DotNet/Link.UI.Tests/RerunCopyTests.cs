using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

/// <summary>
/// The report row offers Rerun (not Resubmit), and both the rerun dialog and the generate form
/// explain what bypassing submission does.
/// </summary>
public class RerunCopyTests
{
    [Fact]
    public void Report_rows_offer_rerun_with_the_clockwise_icon()
    {
        foreach (var relative in new[]
        {
            "DotNet/Link.UI/Views/Reports/Index.cshtml",
            "DotNet/Link.UI/Views/Tenants/_ViewReports.cshtml"
        })
        {
            var text = File.ReadAllText(RepoFile(relative));
            text.Should().Contain("<i class=\"bi bi-arrow-clockwise\" aria-hidden=\"true\"></i><span class=\"ms-1\">Rerun…</span>");
            text.Should().Contain("id=\"resubmitTitle\">Rerun report</h2>");
            text.Should().Contain("A rerun re-evaluates and re-validates this report and regenerates it from the cached EHR data.");
            text.Should().Contain("<strong>Rerun without submitting</strong> bypasses submission, so nothing is sent to NHSN ABS.");
            text.Should().NotContain(">Resubmit<");
            text.Should().NotContain("Resubmit report");
            text.Should().NotContain("bi-arrow-repeat");
        }
    }

    [Fact]
    public void Generate_explains_bypass_submission_next_to_the_choice()
    {
        var text = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Reports/Generate.cshtml"));
        var radios = text.IndexOf("name=\"bypassSubmission\" value=\"true\"", StringComparison.Ordinal);
        var explain = text.IndexOf("data-bypass-explain", StringComparison.Ordinal);

        radios.Should().BeGreaterThan(0);
        explain.Should().BeGreaterThan(radios);
        text.Should().Contain("the generated report is not sent to NHSN Azure Blob Storage (ABS)");
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
