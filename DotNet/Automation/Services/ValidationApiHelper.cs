using LantanaGroup.Link.Automation.Helpers;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Automation.Services;

public class ValidationApiHelper(ValidationServiceClient validationClient, IAutomationOutput output, LokiScraper lokiScraper)
{
    public async Task InitializeArtifactsAsync()
    {
        output.WriteLine("Initializing validation artifacts...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            await validationClient.InitializeArtifactsAsync();
        }, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10), output, lokiScraper);
    }

    public async Task InitializeCategoriesAsync()
    {
        output.WriteLine("Initializing validation categories...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            await validationClient.InitializeCategoriesAsync();
        }, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(10), output, lokiScraper);
    }
}
