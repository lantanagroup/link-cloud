using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Link.UI.Services;
using Xunit;

namespace Link.UI.Tests;

[Collection("api-hosts")]
public sealed class NativeApiClosedTests : IClassFixture<BearerOffHost>
{
    private readonly BearerOffHost _host;

    public NativeApiClosedTests(BearerOffHost host) => _host = host;

    [Theory]
    [InlineData("GET", "/health")]
    public async Task Health_stays_open(string method, string path)
    {
        using var response = await NativeApiHttp.Send(_host.Client, method, path);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET", "/")]
    [InlineData("GET", "/Tenants")]
    [InlineData("GET", "/hubs/runs")]
    [InlineData("GET", "/hubs/cleanup")]
    [InlineData("GET", "/Runs")]
    public async Task Shell_pages_stay_closed(string method, string path)
    {
        using var response = await NativeApiHttp.Send(_host.Client, method, path);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Be(ShellAccessGate.AnonymousBlockedMessage);
    }

    [Theory]
    [InlineData("GET", "/api/runs/metrics")]
    [InlineData("GET", "/api/runs/11111111-1111-1111-1111-111111111111/status")]
    [InlineData("POST", "/api/runs/start")]
    [InlineData("POST", "/api/runs/11111111-1111-1111-1111-111111111111/events/admit")]
    [InlineData("POST", "/api/api-health-runs/start-all")]
    [InlineData("POST", "/api/api-health-runs/start-all-for-pipeline")]
    [InlineData("GET", "/api/api-health-runs/22222222-2222-2222-2222-222222222222/status")]
    [InlineData("GET", "/api/api-health-runs/22222222-2222-2222-2222-222222222222/results")]
    public async Task Native_routes_are_closed_with_no_token_a_bad_token_or_a_test_token(string method, string path)
    {
        using var bare = await NativeApiHttp.Send(_host.Client, method, path);
        bare.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        bare.Headers.Location.Should().BeNull();
        (await bare.Content.ReadAsStringAsync()).Should().Be(ShellAccessGate.AnonymousBlockedMessage);

        using var badClient = _host.CreatePlainClient();
        badClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "bad.token.value");
        using var bad = await NativeApiHttp.Send(badClient, method, path);
        bad.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var validClient = _host.CreatePlainClient();
        _host.Authorize(validClient, "api://link-ui-test");
        using var valid = await NativeApiHttp.Send(validClient, method, path);
        valid.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        valid.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Login_proxy_is_not_closed_by_the_shell()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var response = await _host.Client.GetAsync("/api/login", cts.Token);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            body.Should().NotBe(ShellAccessGate.AnonymousBlockedMessage);
        response.Headers.Location?.OriginalString.Should().NotBe("/api/login");
    }
}

[Collection("api-hosts")]
public sealed class NativeApiClosedWithSessionTests : IClassFixture<BearerOffSignedInHost>
{
    private readonly BearerOffSignedInHost _host;

    public NativeApiClosedWithSessionTests(BearerOffSignedInHost host) => _host = host;

    [Theory]
    [InlineData("GET", "/api/runs/metrics")]
    [InlineData("POST", "/api/api-health-runs/start-all")]
    [InlineData("GET", "/api/api-health-runs/22222222-2222-2222-2222-222222222222/status")]
    public async Task A_shell_session_does_not_open_the_native_api(string method, string path)
    {
        using var response = await NativeApiHttp.Send(_host.Client, method, path);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Be(ShellAccessGate.AnonymousBlockedMessage);
    }
}

[Collection("api-hosts")]
public sealed class BearerOnWithSessionTests : IClassFixture<BearerOnSignedInHost>
{
    private readonly BearerOnSignedInHost _host;

    public BearerOnWithSessionTests(BearerOnSignedInHost host) => _host = host;

    [Fact]
    public async Task A_shell_session_does_not_replace_a_bearer_token()
    {
        using var missing = await _host.Client.GetAsync("/api/runs/metrics");
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        missing.Headers.Location.Should().BeNull();

        using var client = _host.CreatePlainClient();
        _host.Authorize(client, "api://link-ui-test");
        using var authorized = await client.GetAsync("/api/runs/metrics");
        authorized.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection("api-hosts")]
public sealed class BearerOnAnonymousTests : IClassFixture<BearerOnAnonymousHost>
{
    private readonly BearerOnAnonymousHost _host;

    public BearerOnAnonymousTests(BearerOnAnonymousHost host) => _host = host;

    [Fact]
    public async Task Development_with_bearer_keeps_pages_open_and_the_api_on_the_token()
    {
        using var home = await _host.Client.GetAsync("/");
        home.StatusCode.Should().Be(HttpStatusCode.OK);

        using var health = await _host.Client.GetAsync("/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);

        using var missing = await _host.Client.GetAsync("/api/runs/metrics");
        missing.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        missing.Headers.Location.Should().BeNull();

        using var bad = _host.CreatePlainClient();
        bad.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "bad.token.value");
        using var rejected = await bad.GetAsync("/api/runs/metrics");
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var client = _host.CreatePlainClient();
        _host.Authorize(client, "link-ui-test");
        using var authorized = await client.GetAsync("/api/runs/metrics");
        authorized.StatusCode.Should().Be(HttpStatusCode.OK);

        using var start = await _host.Client.PostAsync("/api/api-health-runs/start-all", null);
        start.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var caller = _host.CreatePlainClient();
        _host.AddAntiforgery(caller);
        using var started = await caller.PostAsync("/api/api-health-runs/start-all", null);
        started.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

internal static class CallerToken
{
    public static string? Extract(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? System.Net.WebUtility.HtmlDecode(match.Groups["token"].Value) : null;
    }
}

file static class NativeApiHttp
{
    public static Task<HttpResponseMessage> Send(HttpClient client, string method, string path) =>
        client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
}
