using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Link.UI.Tests;

public class AutomationRunPageScriptTests
{
    [Theory]
    [InlineData("DotNet/Link.UI/wwwroot/js/automation-run.js", "SubscribeRun")]
    [InlineData("DotNet/Link.UI/wwwroot/js/automation-dashboard.js", "SubscribeDashboard")]
    public void Live_page_reads_current_state_after_subscribe_and_reconnect(string relativePath, string subscribeMethod)
    {
        var js = File.ReadAllText(RepoFile(relativePath));

        var catchUp = js.IndexOf("function catchUp()", StringComparison.Ordinal);
        catchUp.Should().BeGreaterThan(0);
        var catchUpBody = js[catchUp..js.IndexOf("connection.on(", catchUp, StringComparison.Ordinal)];
        catchUpBody.Should().Contain($"connection.invoke(\"{subscribeMethod}\"");
        var subscribeAt = catchUpBody.IndexOf($"invoke(\"{subscribeMethod}\"", StringComparison.Ordinal);
        var refreshAt = catchUpBody.IndexOf("refresh()", subscribeAt, StringComparison.Ordinal);
        refreshAt.Should().BeGreaterThan(subscribeAt);

        var start = js.IndexOf("connection.start()", StringComparison.Ordinal);
        start.Should().BeGreaterThan(0);
        var startChain = js[start..];
        startChain.Should().Contain(".then(catchUp)");

        var reconnected = js.IndexOf("connection.onreconnected", StringComparison.Ordinal);
        reconnected.Should().BeGreaterThan(0);
        var reconnectedBody = js[reconnected..js.IndexOf("connection.onclose", reconnected, StringComparison.Ordinal)];
        reconnectedBody.Should().Contain("catchUp()");

        var beforeStart = js[..start];
        var standalone = beforeStart.LastIndexOf("refresh();", StringComparison.Ordinal);
        standalone.Should().BeGreaterThan(catchUp);
        beforeStart[(standalone - 40)..standalone].Should().NotContain("then(function");

        if (relativePath.EndsWith("automation-run.js", StringComparison.Ordinal))
        {
            var poll = js.IndexOf("setInterval(function ()", StringComparison.Ordinal);
            poll.Should().BeGreaterThan(0);
            var pollBody = js[poll..js.IndexOf("refresh();", poll, StringComparison.Ordinal)];
            pollBody.Should().Contain("Succeeded");
            pollBody.Should().Contain("clearInterval");
            js.IndexOf("refresh();", poll, StringComparison.Ordinal).Should().BeLessThan(start);
        }
    }

    [Fact]
    public void Link_ui_settings_set_the_kafka_rest_proxy_address()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(RepoFile("DotNet/Link.UI/appsettings.json"))
            .AddJsonFile(RepoFile("DotNet/Link.UI/appsettings.Development.json"))
            .Build();

        config["Automation:Kafka:RestProxyBaseUrl"].Should().Be("http://localhost:8082");
        config["Automation:FhirServerBase"].Should().Be("http://localhost:6157/fhir");
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
