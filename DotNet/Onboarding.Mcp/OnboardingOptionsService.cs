using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed class OnboardingOptionsService(HttpClient httpClient, IOptions<OnboardingOptions> options)
{
    public async Task<OnboardingOptionsResult> GetOnboardingOptions(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = options.Value;
        var vendorsTask = FetchAsync(configuration.TenantApiBaseUrl, configuration.BearerToken,
            "Vendor", "tenant_api", "Tenant", ParseVendor, cancellationToken);
        var versionsTask = FetchAsync(configuration.TenantApiBaseUrl, configuration.BearerToken,
            "VendorVersion", "tenant_api", "Tenant", ParseVersion, cancellationToken);
        var definitionsTask = FetchAsync(configuration.MeasureEvalApiBaseUrl, configuration.MeasureEvalBearerToken,
            "measure-definition", "measureeval_api", "MeasureEval", ParseMeasureDefinition, cancellationToken);
        await Task.WhenAll(vendorsTask, versionsTask, definitionsTask);
        var vendors = await vendorsTask;
        var versions = await versionsTask;
        var definitions = await definitionsTask;
        var status = vendors.Status == "available" && versions.Status == "available" && definitions.Status == "available"
            ? "complete" : "partial";
        return new(status, Configured(configuration.ConnectionProfiles), Configured(configuration.QueryPlanProfiles),
            definitions, vendors, versions);
    }

    private static SourceResult<T> Configured<T>(T[] values) => new(
        values.Length == 0 ? "not_configured" : "configured", "local_configuration", values,
        "Operator-maintained catalog; not verified against the running services.");

    private async Task<SourceResult<T>> FetchAsync<T>(string baseUrl, string? bearerToken,
        string route, string source, string serviceName,
        Func<JsonElement, T> parse, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), route));
            if (!string.IsNullOrWhiteSpace(bearerToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new("unavailable", source, [], $"{serviceName} returned HTTP {(int)response.StatusCode}.");
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var items = document.RootElement.EnumerateArray().Select(parse).ToArray();
            return new("available", source, items);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new("unavailable", source, [], $"{serviceName} request timed out.");
        }
        catch (HttpRequestException)
        {
            return new("unavailable", source, [], $"Cannot reach the configured {serviceName} API.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            return new("unavailable", source, [], $"{serviceName} returned an unexpected catalog response.");
        }
    }

    // Explicit projection prevents vendor authentication settings from reaching the model.
    private static VendorOption ParseVendor(JsonElement item) =>
        new(item.GetProperty("id").GetGuid(), item.GetProperty("name").GetString() ?? "");
    private static VendorVersionOption ParseVersion(JsonElement item) =>
        new(item.GetProperty("id").GetGuid(), item.GetProperty("vendorId").GetGuid(),
            item.GetProperty("version").GetString() ?? "");

    // MeasureEval's list endpoint returns summary entities (id/version), not FHIR Bundles.
    private static MeasureDefinitionOption ParseMeasureDefinition(JsonElement item)
    {
        var id = item.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id)) throw new JsonException("Missing measure definition ID.");
        return new(id, item.GetProperty("version").GetInt64());
    }
}
