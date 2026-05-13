using LantanaGroup.Link.Automation.Link.Helpers;
using LantanaGroup.Link.Sdk.Clients;

namespace LantanaGroup.Link.Automation.Link.Services;

public class ValidationApiHelper(IValidationServiceClient validationClient, IAutomationOutput output, LokiScraper lokiScraper)
{
    public async Task InitializeArtifactsAsync()
    {
        if (await validationClient.HasArtifactsAsync())
        {
            output.WriteLine("Validation artifacts already initialized. Skipping initialize call.");
            return;
        }

        output.WriteLine("Initializing validation artifacts...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            await validationClient.InitializeArtifactsAsync();
        }, TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(10), output,
            onFinalFailureDiagnostics: () => lokiScraper.ScrapeErrorsAsync());
    }

    public async Task InitializeCategoriesAsync()
    {
        if (await validationClient.HasCategoriesAsync())
        {
            output.WriteLine("Validation categories already initialized. Skipping initialize call.");
            return;
        }

        output.WriteLine("Initializing validation categories...");
        await RetryHelper.RetryUntilSuccess(async () =>
        {
            await validationClient.InitializeCategoriesAsync();
        }, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(10), output,
            onFinalFailureDiagnostics: () => lokiScraper.ScrapeErrorsAsync());
    }
}
