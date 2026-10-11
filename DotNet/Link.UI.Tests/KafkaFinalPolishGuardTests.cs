using FluentAssertions;
using Xunit;

namespace Link.UI.Tests;

public class KafkaFinalPolishGuardTests
{
    private static string Ops(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "Link.UI", "Views", "Operations")))
            dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(dir!, "Link.UI", "Views", "Operations", file));
    }

    [Theory]
    [InlineData("_Consumers.cshtml")]
    [InlineData("_Brokers.cshtml")]
    [InlineData("_Messages.cshtml")]
    public void Search_has_no_loose_label_line(string file)
    {
        var view = Ops(file);
        view.Should().NotContain("class=\"form-label\" for=\"kafka-q\">Search</label>");
        view.Should().NotContain("class=\"form-label\" for=\"browse-q\">Search</label>");
        view.Should().Contain("placeholder=\"Search");
    }

    [Fact]
    public void Partitions_does_not_repeat_the_summary_link_and_picker_has_no_loose_count()
    {
        Ops("_Migrate.cshtml").Should().NotContain(">Topic summary</a>");
        Ops("_TopicPicker.cshtml").Should().NotContain("topics.</p>");
        Ops("Kafka.cshtml").Should().Contain("lu-provider-badge");
    }
}
