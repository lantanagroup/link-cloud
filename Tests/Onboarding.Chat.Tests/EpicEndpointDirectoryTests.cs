using System.Net;
using System.Text;
using LantanaGroup.Link.Onboarding.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Xunit;

namespace Onboarding.Chat.Tests;

public sealed class EpicEndpointDirectoryTests
{
    private const string Bundle = """
        {"resourceType":"Bundle","entry":[
          {"fullUrl":"urn:uuid:brand","resource":{"resourceType":"Organization","id":"brand","name":"Example Health","endpoint":[{"reference":"urn:uuid:endpoint"}]}},
          {"resource":{"resourceType":"Organization","id":"facility","name":"Example Hospital","address":[{"city":"Madison","state":"WI"}],"partOf":{"reference":"Organization/brand"}}},
          {"fullUrl":"urn:uuid:endpoint","resource":{"resourceType":"Endpoint","id":"endpoint","status":"active","connectionType":{"code":"hl7-fhir-rest"},"address":"https://ehr.example/FHIR/R4"}}
        ]}
        """;

    private sealed class Handler : HttpMessageHandler
    {
        public string Body = Bundle;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public int Requests;
        public bool Timeout;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Timeout) throw new TaskCanceledException();
            Assert.Equal(EpicEndpointDirectory.DownloadUrl, request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Requests++;
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ReportsLoadingTimeoutAndHttpFailureBeforeFirstSuccessfulDownload()
    {
        var handler = new Handler { Timeout = true };
        using var client = new HttpClient(handler);
        using var directory = new EpicEndpointDirectory(client, NullLogger<EpicEndpointDirectory>.Instance);
        Assert.Contains("still downloading", directory.Lookup("example", 1, 10, default).Message);
        Assert.False(await directory.RefreshAsync(default));
        Assert.Contains("timed out", directory.Lookup("example", 1, 10, default).Message);
        handler.Timeout = false;
        handler.Status = HttpStatusCode.ServiceUnavailable;
        Assert.False(await directory.RefreshAsync(default));
        Assert.Contains("retry runs in one minute", directory.Lookup("example", 1, 10, default).Message);
        handler.Status = HttpStatusCode.OK;
        Assert.True(await directory.RefreshAsync(default));
        Assert.Equal("available", directory.Lookup("example", 1, 10, default).Status);
    }

    [Fact]
    public async Task ResolvesFacilityBrandReferencesAndSearchesCachedSnapshot()
    {
        var handler = new Handler();
        using var client = new HttpClient(handler);
        using var directory = new EpicEndpointDirectory(client, NullLogger<EpicEndpointDirectory>.Instance);
        Assert.Equal("unavailable", directory.Lookup("Example", 1, 10, default).Status);
        Assert.True(await directory.RefreshAsync(default));
        var result = directory.Lookup("hospital WI", 1, 10, default);
        Assert.Equal("available", result.Status);
        var match = Assert.Single(result.Records);
        Assert.Equal("Example Health", match.BrandName);
        Assert.Equal("Madison", match.City);
        Assert.Equal("https://ehr.example/FHIR/R4", match.FhirServerBaseUrl);
        Assert.NotNull(result.RetrievedAt);
        Assert.Equal(2, directory.Lookup("example", 1, 1, default).Metadata.TotalPages);
        Assert.Empty(directory.Lookup("unknown", 1, 10, default).Records);
        Assert.Empty(directory.Lookup("example", int.MaxValue, 50, default).Records);
        Assert.Equal(1, handler.Requests);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"resourceType\":\"Bundle\",\"entry\":[]}")]
    [InlineData("{\"resourceType\":\"OperationOutcome\"}")]
    public async Task FailedRefreshRetainsPreviousSnapshot(string invalidBody)
    {
        var handler = new Handler();
        using var client = new HttpClient(handler);
        using var directory = new EpicEndpointDirectory(client, NullLogger<EpicEndpointDirectory>.Instance);
        Assert.True(await directory.RefreshAsync(default));
        var timestamp = directory.Lookup("example", 1, 10, default).RetrievedAt;
        handler.Body = invalidBody;
        Assert.False(await directory.RefreshAsync(default));
        Assert.Equal(timestamp, directory.Lookup("example", 1, 10, default).RetrievedAt);
        handler.Status = HttpStatusCode.ServiceUnavailable;
        Assert.False(await directory.RefreshAsync(default));
        Assert.Equal(2, directory.Lookup("example", 1, 10, default).Records.Length);
    }

    [Fact]
    public async Task RejectsUnsafeEndpointsInvalidInputsAndCancellation()
    {
        var handler = new Handler { Body = Bundle.Replace("https://ehr.example/FHIR/R4", "https://user:secret@ehr.example/FHIR/R4") };
        using var client = new HttpClient(handler);
        using var directory = new EpicEndpointDirectory(client, NullLogger<EpicEndpointDirectory>.Instance);
        Assert.False(await directory.RefreshAsync(default));
        Assert.Throws<McpException>(() => directory.Lookup("", 1, 10, default));
        Assert.Throws<McpException>(() => directory.Lookup("example", 0, 10, default));
        Assert.Throws<McpException>(() => directory.Lookup("example", 1, 51, default));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => directory.Lookup("example", 1, 10, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => directory.RefreshAsync(cts.Token));
    }
}
