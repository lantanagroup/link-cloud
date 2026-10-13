using System.Text.Json;
using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

public class KafkaBrokerLifecycleFlagTests
{
    [Theory]
    [InlineData("{\"provider\":\"Disabled\",\"enabled\":false,\"detail\":\"\"}", false)]
    [InlineData("{\"provider\":\"Strimzi\",\"enabled\":true,\"brokerLifecycleEnabled\":false}", false)]
    [InlineData("{\"provider\":\"Strimzi\",\"enabled\":true,\"brokerLifecycleEnabled\":true}", true)]
    public void Infra_status_reads_the_flag_and_defaults_to_off(string json, bool expected)
    {
        var status = JsonSerializer.Deserialize<InfraStatus>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        status.BrokerLifecycleEnabled.Should().Be(expected);
        new InfraStatus().BrokerLifecycleEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("DotNet/Link.UI/Views/Operations/_Brokers.cshtml", "data-bs-target=\"#kafka-add-broker\"")]
    [InlineData("DotNet/Link.UI/Views/Operations/_Brokers.cshtml", "<div class=\"collapse mb-3\" id=\"kafka-add-broker\">")]
    [InlineData("DotNet/Link.UI/Views/Operations/_Brokers.cshtml", "<form method=\"post\" action=\"/Operations/Kafka/brokers/decommission/plan\"")]
    [InlineData("DotNet/Link.UI/Views/Operations/_Consumers.cshtml", "<form method=\"post\" action=\"/Operations/Kafka/groups/replicas/plan\"")]
    public void Broker_and_pod_actions_render_only_when_the_flag_is_on(string view, string action)
    {
        var text = File.ReadAllText(RepoFile(view)).Replace("\r\n", "\n");
        var at = text.IndexOf(action, StringComparison.Ordinal);
        at.Should().BeGreaterThan(0);
        var before = text[..at];
        var guard = before.LastIndexOf("@if (Model.Infra.BrokerLifecycleEnabled)", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(0, "the action must sit inside the BrokerLifecycleEnabled block");
        before[guard..].Count(ch => ch == '{').Should().BeGreaterThan(before[guard..].Count(ch => ch == '}'), "the guard block must still be open at the action");
    }

    [Fact]
    public void Partition_and_replication_factor_actions_are_not_behind_the_flag()
    {
        var root = RepoFile("DotNet/Link.UI/Views/Operations");
        foreach (var file in Directory.GetFiles(root, "*.cshtml"))
        {
            var text = File.ReadAllText(file);
            foreach (var action in new[] { "/partitions/plan", "/replication-factor" })
            {
                var at = text.IndexOf(action, StringComparison.Ordinal);
                if (at < 0)
                    continue;
                var before = text[..at];
                var guard = before.LastIndexOf("@if (Model.Infra.BrokerLifecycleEnabled)", StringComparison.Ordinal);
                if (guard >= 0)
                    before[guard..].Count(ch => ch == '{').Should().BeLessThanOrEqualTo(before[guard..].Count(ch => ch == '}'), Path.GetFileName(file) + " " + action);
            }
        }
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