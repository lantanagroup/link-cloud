using System.Net;
using System.Text;
using Automation.UI.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class PrometheusHistogramClientTests
{
    [Fact]
    public async Task QueryScalar_reads_prometheus_vector_value()
    {
        var json = """
            {"status":"success","data":{"resultType":"vector","result":[{"metric":{},"value":[1788368940.517,"11"]}]}}
            """;
        var client = CreateClient((_, _) => Json(json));

        var value = await client.QueryScalarAsync(
            "sum(link_data_acq_query_duration_milliseconds_count{facility_id=\"abc\"})",
            DateTimeOffset.UtcNow);

        value.Should().Be(11);
    }

    [Fact]
    public async Task QueryScalar_returns_null_for_empty_vector()
    {
        var json = """{"status":"success","data":{"resultType":"vector","result":[]}}""";
        var client = CreateClient((_, _) => Json(json));

        var value = await client.QueryScalarAsync("sum(up)", DateTimeOffset.UtcNow);

        value.Should().BeNull();
    }

    [Fact]
    public async Task QueryScalar_returns_null_for_nan()
    {
        var json = """
            {"status":"success","data":{"resultType":"vector","result":[{"metric":{},"value":[1,"NaN"]}]}}
            """;
        var client = CreateClient((_, _) => Json(json));

        var value = await client.QueryScalarAsync("histogram_quantile(0.95, sum by (le) (x_bucket))", DateTimeOffset.UtcNow);

        value.Should().BeNull();
    }

    [Fact]
    public async Task IsReachable_is_true_on_buildinfo_200()
    {
        var client = CreateClient((request, _) =>
        {
            request.RequestUri!.AbsolutePath.Should().Be("/api/v1/status/buildinfo");
            return Json("""{"status":"success","data":{}}""");
        });

        (await client.IsReachableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task IsReachable_is_false_when_host_cannot_be_resolved()
    {
        var client = CreateClient((_, _) => throw new HttpRequestException("No such host is known."));

        (await client.IsReachableAsync()).Should().BeFalse();
    }

    [Fact]
    public void ResolveQueryEndpoint_rewrites_unresolvable_prometheus_host_to_localhost()
    {
        PrometheusHistogramClient.ResolveQueryEndpoint("http://prometheus:9090", _ => false)
            .Should().Be("http://localhost:9090");
    }

    [Fact]
    public void ResolveQueryEndpoint_keeps_prometheus_when_the_name_resolves()
    {
        PrometheusHistogramClient.ResolveQueryEndpoint("http://prometheus:9090", _ => true)
            .Should().Be("http://prometheus:9090");
    }

    [Fact]
    public void ResolveQueryEndpoint_leaves_localhost_alone()
    {
        PrometheusHistogramClient.ResolveQueryEndpoint("http://localhost:9090/", _ => false)
            .Should().Be("http://localhost:9090");
    }

    private static PrometheusHistogramClient CreateClient(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
    {
        var http = new HttpClient(new StubHandler(responder))
        {
            BaseAddress = new Uri("http://localhost:9090/")
        };
        return new PrometheusHistogramClient(http, NullLogger<PrometheusHistogramClient>.Instance);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request, cancellationToken));
    }
}
