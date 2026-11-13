using Automation.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AutomationSerilogTests
{
    [Fact]
    public void Empty_loki_uri_does_not_count_as_a_sink()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:WriteTo:0:Name"] = "GrafanaLoki",
                ["Serilog:WriteTo:0:Args:uri"] = "",
                ["Serilog:WriteTo:1:Name"] = "Console"
            })
            .Build();

        AutomationSerilog.HasLokiSink(configuration).Should().BeFalse();
    }

    [Fact]
    public void A_grafana_loki_entry_with_a_uri_enables_the_sink()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:WriteTo:0:Name"] = "Console",
                ["Serilog:WriteTo:1:Name"] = "GrafanaLoki",
                ["Serilog:WriteTo:1:Args:uri"] = "http://loki:3100"
            })
            .Build();

        AutomationSerilog.HasLokiSink(configuration).Should().BeTrue();
    }

    [Fact]
    public void Whitespace_uri_stays_disabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Serilog:WriteTo:0:Name"] = "GrafanaLoki",
                ["Serilog:WriteTo:0:Args:uri"] = "   "
            })
            .Build();

        AutomationSerilog.HasLokiSink(configuration).Should().BeFalse();
    }
}
