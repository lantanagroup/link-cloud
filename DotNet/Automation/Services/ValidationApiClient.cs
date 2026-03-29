using System.Net;
using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Automation.Services;

public class ValidationApiClient(ValidationServiceClient validationClient, IAutomationOutput output, LokiScraper lokiScraper)
{
    public async Task InitializeArtifactsAsync()
    {
        output.WriteLine("Initializing validation artifacts...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            var status = await validationClient.InitializeArtifactsAsync();
            AutomationInvariant.Require(status == HttpStatusCode.OK,
                $"Initialize Validation Artifacts - Expected HTTP 200 OK but received {status}.");
        }, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10), output, lokiScraper);
    }

    public async Task InitializeCategoriesAsync()
    {
        output.WriteLine("Initializing validation categories...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            var status = await validationClient.InitializeCategoriesAsync();
            AutomationInvariant.Require(status == HttpStatusCode.OK,
                $"Initialize Validation Categories - Expected HTTP 200 OK but received {status}.");
        }, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(10), output, lokiScraper);
    }
}
