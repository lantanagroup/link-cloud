using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class KafkaConsumerLayoutTests
{
    [Fact]
    public void Group_column_keeps_a_readable_width_and_assignments_wrap()
    {
        var css = File.ReadAllText(RepoFile("DotNet/Link.UI/wwwroot/css/site.css"));
        css.Should().Contain("#kafka-group-table > :is(thead, tbody) > tr > :is(th, td):first-child {");

        var view = File.ReadAllText(RepoFile("DotNet/Link.UI/Views/Operations/_Consumers.cshtml"));
        view.Should().Contain("<th class=\"lu-wrap\">Assignment</th>");
        view.Should().Contain("<td class=\"lu-wrap\">@KafkaWindows.AssignmentText(member.Assignment)</td>");
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