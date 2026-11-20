using System.Text;
using Flurl.Http.Testing;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Extensions.Security;
using LantanaGroup.Link.Shared.Application.Interfaces.Services.Security.Token;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.LinkSdk;

/// <summary>
/// <see cref="IReportServiceClient.GetScheduleManifestAsync"/> against Report's
/// <c>GET /api/schedules/{id}/manifest</c>. The body is NDJSON or a FHIR Bundle, neither of which this
/// client's JSON settings can deserialize, so it comes back as text with its content type.
/// </summary>
[Trait("Category", "UnitTests")]
public class ReportServiceClientManifestTests
{
    private const string ScheduleId = "7d9f7c1e-3b1a-4c55-9d7e-2f1e0f4a6b10";
    private const string BaseUrl = "http://link.test";

    [Fact]
    public async Task GetScheduleManifestAsync_Defaults_RequestsNdjsonWithoutForce()
    {
        const string ndjson = "{\"resourceType\":\"Organization\"}\n{\"resourceType\":\"Device\"}\n";
        using var http = new HttpTest();
        http.RespondWith(() => new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson"));
        using var client = CreateClient(BaseUrl);

        var response = await client.GetScheduleManifestAsync(ScheduleId);

        var call = Assert.Single(http.CallLog);
        var url = new Uri(call.Request.Url.ToString());
        Assert.Equal("GET", call.Request.Verb.Method);
        Assert.Equal($"/api/schedules/{ScheduleId}/manifest", url.AbsolutePath);
        Assert.Contains("format=ndjson", url.Query);
        Assert.Contains("force=false", url.Query);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal(ndjson, response.Body);
        Assert.StartsWith("application/x-ndjson", response.ContentType);
    }

    [Fact]
    public async Task GetScheduleManifestAsync_JsonAndForce_SendsBothQueryParameters()
    {
        const string bundle = "{\"resourceType\":\"Bundle\",\"type\":\"collection\",\"entry\":[]}";
        using var http = new HttpTest();
        http.RespondWith(() => new StringContent(bundle, Encoding.UTF8, "application/fhir+json"));
        using var client = CreateClient(BaseUrl);

        var response = await client.GetScheduleManifestAsync(ScheduleId, ReportManifestFormat.Json, force: true);

        var url = new Uri(Assert.Single(http.CallLog).Request.Url.ToString());
        Assert.Contains("format=json", url.Query);
        Assert.Contains("force=true", url.Query);
        Assert.Equal(bundle, response.Body);
        Assert.StartsWith("application/fhir+json", response.ContentType);
    }

    /// <summary>
    /// The endpoint's refusals are Problem Details. They come back as a failed response with the
    /// payload in RawBody, not as an exception and not as a manifest body.
    /// </summary>
    [Fact]
    public async Task GetScheduleManifestAsync_PeriodNotClosed_ReturnsProblemAsRawBody()
    {
        const string problem = "{\"status\":409,\"detail\":\"The reporting period has not closed.\"}";
        using var http = new HttpTest();
        http.RespondWith(() => new StringContent(problem, Encoding.UTF8, "application/problem+json"), 409);
        using var client = CreateClient(BaseUrl);

        var response = await client.GetScheduleManifestAsync(ScheduleId);

        Assert.Equal(409, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
        Assert.Null(response.Body);
        Assert.Equal(problem, response.RawBody);
        Assert.StartsWith("application/problem+json", response.ContentType);
    }

    /// <summary>
    /// Against a real loopback socket: a token the caller has already cancelled stops the call before
    /// any request reaches the server. Like every method on <c>LinkApiClientBase</c>, the client reports
    /// a call that never completed as an unsuccessful response with status 0 rather than throwing.
    /// </summary>
    [Fact]
    public async Task GetScheduleManifestAsync_CallerCancelled_DoesNotSendTheRequest()
    {
        using var server = new OneShotServer("{}");
        using var client = CreateClient(server.BaseUrl);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var response = await client.GetScheduleManifestAsync(ScheduleId, cancellationToken: cts.Token);

        Assert.Equal(0, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
        Assert.Null(response.Body);
        await Assert.ThrowsAsync<TimeoutException>(
            () => server.WaitForRequestAsync(TimeSpan.FromMilliseconds(500)));
    }

    private static ReportServiceClient CreateClient(string baseUrl) =>
        new(
            Options.Create(new ServiceRegistry { ReportServiceUrl = baseUrl }),
            Options.Create(new BackendAuthenticationServiceExtension.LinkBearerServiceOptions { AllowAnonymous = true }),
            Options.Create(new LinkTokenServiceSettings { SigningKey = "test" }),
            new Mock<ICreateSystemToken>().Object);
}
