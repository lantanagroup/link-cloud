using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol;

namespace LantanaGroup.Link.Onboarding.Mcp;

public sealed class OnboardingOptionsService(HttpClient httpClient, IOptions<OnboardingOptions> options)
{
    public async Task<OnboardingOptionsResult> GetAsync(string? environmentId, CancellationToken cancellationToken)
    {
        var environments = options.Value.Environments
            .Where(e => environmentId is null || string.Equals(e.Id, environmentId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (environments.Length == 0)
            throw new McpException("Unknown environment ID. Omit environmentId to list configured environments.");

        var results = new List<EnvironmentResult>();
        foreach (var environment in environments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vendorsTask = FetchAsync(environment.TenantApiBaseUrl, environment.BearerToken,
                "Vendor", "tenant_api", "Tenant", ParseVendor, cancellationToken);
            var versionsTask = FetchAsync(environment.TenantApiBaseUrl, environment.BearerToken,
                "VendorVersion", "tenant_api", "Tenant", ParseVersion, cancellationToken);
            var definitionsTask = FetchAsync(environment.MeasureEvalApiBaseUrl, environment.MeasureEvalBearerToken,
                "measure-definition", "measureeval_api", "MeasureEval", ParseMeasureDefinition, cancellationToken);
            await Task.WhenAll(vendorsTask, versionsTask, definitionsTask);
            results.Add(new(environment.Id, environment.DisplayName,
                Configured(environment.ConnectionProfiles), Configured(environment.QueryPlanProfiles),
                await definitionsTask, await vendorsTask, await versionsTask));
        }
        return new(results.All(e => e.Vendors.Status == "available" && e.VendorVersions.Status == "available"
                && e.MeasureDefinitions.Status == "available")
            ? "complete" : "partial", results.ToArray());
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
