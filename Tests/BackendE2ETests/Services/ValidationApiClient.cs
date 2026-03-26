using System.Net;
using LantanaGroup.Link.Tests.E2ETests.Helpers;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Services;

public class ValidationApiClient(RestClient client, ITestOutputHelper output, LokiScraper lokiScraper)
{
    public async Task InitializeArtifactsAsync()
    {
        output.WriteLine("Initializing validation artifacts...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            var request = new RestRequest("validation/artifact/$initialize", Method.Post);
            request.Timeout = TimeSpan.FromSeconds(120);
            var response = await client.ExecuteAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Initialize Validation Artifacts - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}.");
        }, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10), output, lokiScraper);
    }

    public async Task InitializeCategoriesAsync()
    {
        output.WriteLine("Initializing validation categories...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            var request = new RestRequest("validation/category/$initialize", Method.Post);
            request.Timeout = TimeSpan.FromSeconds(60);
            var response = await client.ExecuteAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Initialize Validation Categories - Expected HTTP 200 OK but received {response.StatusCode}: {response.Content}.");
        }, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(10), output, lokiScraper);
    }
}
