using System.Net;
using System.Text;
using System.Text.Json;
using LantanaGroup.Link.Onboarding.Mcp;
using ModelContextProtocol;
using Xunit;

namespace Onboarding.Chat.Tests;

public sealed class LuxeraEndpointApiTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Handler : HttpMessageHandler, IHttpClientFactory
    {
        public readonly List<Uri> Requests = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond =
            (_, _) => Task.FromResult(Page(1, 1));
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(LuxeraEndpointApi.ClientName, name);
            return new(this, disposeHandler: false);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Null(request.Headers.Authorization);
            Requests.Add(request.RequestUri!);
            return Respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Page(int page, int total, string url = "https://ehr.example/fhir")
    {
        var start = (page - 1) * 100;
        var data = Enumerable.Range(start, Math.Min(100, Math.Max(0, total - start)))
            .Select(i => new { organizationName = $"Orlando Health {i}", url, vendorName = "Epic Systems", vendorId = "epic",
                city = "Orlando", state = "FL", fhirVersion = "R4", access = new[] { "patient" }, status = "verified", lastUpdated = "2026-10-19T00:00:00Z" });
        return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(
            new { success = true, data, meta = new { total, page, limit = 100 } }), Encoding.UTF8, "application/json") };
    }

    [Fact]
    public async Task SearchesLuxeraWithEscapedQueryAndCachesUpstreamPagesForOneHour()
    {
        var clock = new Clock();
        var handler = new Handler { Respond = (_, _) => Task.FromResult(Page(1, 100)) };
        using var directory = new LuxeraEndpointApi(handler, clock);
        var first = await directory.SearchAsync(" Orlando & Health ", 1, 10, default);
        Assert.Equal("available", first.Status);
        Assert.Equal("https://fhir-api.luxera.io/api/v1/endpoints?vendor=epic&query=orlando%20%26%20health&page=1&limit=100", handler.Requests.Single().AbsoluteUri);
        Assert.Equal(100, first.Metadata.TotalCount);
        Assert.Equal(10, first.Metadata.TotalPages);
        Assert.Equal("Epic Systems", first.Records[0].VendorName);
        Assert.Equal(new[] { "patient" }, first.Records[0].Access);
        Assert.NotNull(first.RetrievedAt);
        var second = await directory.SearchAsync("ORLANDO & HEALTH", 2, 10, default);
        Assert.Equal("Orlando Health 10", second.Records[0].OrganizationName);
        Assert.Single(handler.Requests);
        clock.Now = clock.Now.AddMinutes(59);
        await directory.SearchAsync("orlando & health", 1, 10, default);
        Assert.Single(handler.Requests);
        clock.Now = clock.Now.AddMinutes(1);
        await directory.SearchAsync("orlando & health", 1, 10, default);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SlicesAcrossUpstreamPagesWithoutDownloadingWholeDirectory()
    {
        var handler = new Handler { Respond = (request, _) => Task.FromResult(Page(request.RequestUri!.Query.Contains("page=2") ? 2 : 1, 200)) };
        using var directory = new LuxeraEndpointApi(handler, new Clock());
        var result = await directory.SearchAsync("orlando", 5, 24, default);
        Assert.Equal("available", result.Status);
        Assert.Equal(24, result.Records.Length);
        Assert.Equal("Orlando Health 96", result.Records[0].OrganizationName);
        Assert.Equal("Orlando Health 119", result.Records[^1].OrganizationName);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task CoalescesConcurrentRequestsAndKeepsEmptyResultsSuccessful()
    {
        var handler = new Handler { Respond = async (_, token) => { await Task.Delay(20, token); return Page(1, 0); } };
        using var directory = new LuxeraEndpointApi(handler, new Clock());
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => directory.SearchAsync("absent", 1, 10, default)));
        Assert.All(results, result => { Assert.Equal("available", result.Status); Assert.Empty(result.Records); Assert.Equal(0, result.Metadata.TotalPages); });
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RespectsRetryAfterAcrossAllSearchesWithoutAutomaticRetry(bool httpDate)
    {
        var clock = new Clock();
        var handler = new Handler();
        handler.Respond = (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = httpDate
                ? new(clock.Now.AddSeconds(45)) : new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
            return Task.FromResult(response);
        };
        using var directory = new LuxeraEndpointApi(handler, clock);
        var result = await directory.SearchAsync("orlando", 1, 10, default);
        Assert.Equal("rate_limited", result.Status);
        Assert.Equal(45, result.RetryAfterSeconds);
        Assert.Equal("rate_limited", (await directory.SearchAsync("another", 1, 10, default)).Status);
        Assert.Single(handler.Requests);
        clock.Now = clock.Now.AddSeconds(45);
        handler.Respond = (_, _) => Task.FromResult(Page(1, 1));
        Assert.Equal("available", (await directory.SearchAsync("orlando", 1, 10, default)).Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task HonorsQuotaResetAndServesCacheDuringCooldown()
    {
        var clock = new Clock();
        var handler = new Handler();
        handler.Respond = (_, _) =>
        {
            var response = Page(1, 100);
            response.Headers.Add("RateLimit-Remaining", "0");
            response.Headers.Add("RateLimit-Reset", clock.Now.AddMinutes(2).ToUnixTimeSeconds().ToString());
            return Task.FromResult(response);
        };
        using var directory = new LuxeraEndpointApi(handler, clock);
        Assert.Equal("available", (await directory.SearchAsync("orlando", 1, 10, default)).Status);
        Assert.Equal("available", (await directory.SearchAsync("orlando", 2, 10, default)).Status);
        var blocked = await directory.SearchAsync("another", 1, 10, default);
        Assert.Equal("rate_limited", blocked.Status);
        Assert.Equal(120, blocked.RetryAfterSeconds);
        Assert.Single(handler.Requests);
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Equal("available", (await directory.SearchAsync("another", 1, 10, default)).Status);
    }

    [Fact]
    public async Task PacesRequestsUsingAdvertisedRemainingQuota()
    {
        var clock = new Clock();
        var handler = new Handler();
        handler.Respond = (_, _) =>
        {
            var response = Page(1, 1);
            response.Headers.Add("RateLimit-Remaining", "1");
            response.Headers.Add("RateLimit-Reset", clock.Now.AddSeconds(60).ToUnixTimeSeconds().ToString());
            return Task.FromResult(response);
        };
        using var directory = new LuxeraEndpointApi(handler, clock);
        await directory.SearchAsync("one", 1, 10, default);
        Assert.Equal(30, (await directory.SearchAsync("two", 1, 10, default)).RetryAfterSeconds);
        clock.Now = clock.Now.AddSeconds(30);
        Assert.Equal("available", (await directory.SearchAsync("two", 1, 10, default)).Status);
    }

    [Fact]
    public async Task UsesLaterQuotaResetWhenItExceedsRetryAfter()
    {
        var clock = new Clock();
        var handler = new Handler();
        handler.Respond = (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(10));
            response.Headers.Add("RateLimit-Remaining", "0");
            response.Headers.Add("RateLimit-Reset", clock.Now.AddSeconds(90).ToUnixTimeSeconds().ToString());
            return Task.FromResult(response);
        };
        using var directory = new LuxeraEndpointApi(handler, clock);
        Assert.Equal(90, (await directory.SearchAsync("orlando", 1, 10, default)).RetryAfterSeconds);
        clock.Now = clock.Now.AddSeconds(10);
        Assert.Equal(80, (await directory.SearchAsync("another", 1, 10, default)).RetryAfterSeconds);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MissingRetryHeadersUseConservativeCooldown()
    {
        var handler = new Handler { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)) };
        using var directory = new LuxeraEndpointApi(handler, new Clock());
        Assert.Equal(60, (await directory.SearchAsync("orlando", 1, 10, default)).RetryAfterSeconds);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"success\":false}")]
    [InlineData("{\"success\":true,\"data\":[],\"meta\":{\"total\":0,\"page\":2,\"limit\":100}}")]
    public async Task InvalidResponsesAreUnavailableAndNotCached(string body)
    {
        var handler = new Handler { Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }) };
        using var directory = new LuxeraEndpointApi(handler, new Clock());
        Assert.Equal("unavailable", (await directory.SearchAsync("orlando", 1, 10, default)).Status);
        handler.Respond = (_, _) => Task.FromResult(Page(1, 1));
        Assert.Equal("available", (await directory.SearchAsync("orlando", 1, 10, default)).Status);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task RejectsUnsafeEndpointAndHandlesHttpFailureAndTimeout()
    {
        var handler = new Handler { Respond = (_, _) => Task.FromResult(Page(1, 1, "https://user:secret@ehr.example/fhir")) };
        using var directory = new LuxeraEndpointApi(handler, new Clock());
        Assert.Equal("unavailable", (await directory.SearchAsync("orlando", 1, 10, default)).Status);
        handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Equal("unavailable", (await directory.SearchAsync("orlando", 1, 10, default)).Status);
        handler.Respond = (_, _) => throw new TaskCanceledException();
        Assert.Contains("timed out", (await directory.SearchAsync("orlando", 1, 10, default)).Message);
    }

    [Fact]
    public async Task ValidatesInputAndForwardsCancellationToHttp()
    {
        var handler = new Handler();
        using var directory = new LuxeraEndpointApi(handler, new Clock());
        await Assert.ThrowsAsync<McpException>(() => directory.SearchAsync("", 1, 10, default));
        await Assert.ThrowsAsync<McpException>(() => directory.SearchAsync("orlando", 0, 10, default));
        await Assert.ThrowsAsync<McpException>(() => directory.SearchAsync("orlando", 1, 26, default));
        await Assert.ThrowsAsync<McpException>(() => directory.SearchAsync("orlando", int.MaxValue, 10, default));
        Assert.Empty(handler.Requests);
        using var cancellation = new CancellationTokenSource();
        handler.Respond = (_, token) => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); throw new Exception("Token not forwarded"); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.SearchAsync("orlando", 1, 10, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.SearchAsync("orlando", 1, 10, cancellation.Token));
        Assert.Single(handler.Requests);
    }
}
