using System.Text.Json;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace Automation.UI.Services;

public interface ITerminologyCodeLookup
{
    Task<GenerationCatalogItem?> LookupAsync(
        GenerationCatalogKind kind,
        string code,
        string? system,
        CancellationToken cancellationToken = default);
}

public sealed class TerminologyCodeLookup(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceRegistry> serviceRegistry,
    ILogger<TerminologyCodeLookup> logger) : ITerminologyCodeLookup
{
    public async Task<GenerationCatalogItem?> LookupAsync(
        GenerationCatalogKind kind,
        string code,
        string? system,
        CancellationToken cancellationToken = default)
    {
        code = (code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var guessed = GenerationCatalogItem.GuessSystem(kind, system, code);
        var systems = DistinctSystems(kind, guessed);
        var baseUrl = serviceRegistry.Value.TerminologyServiceUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return Fallback(kind, code, guessed);

        var client = httpClientFactory.CreateClient("TerminologyLookup");
        foreach (var sys in systems)
        {
            try
            {
                var url = $"{baseUrl}/api/terminology/fhir/CodeSystem/$lookup?system={Uri.EscapeDataString(sys)}&code={Uri.EscapeDataString(code)}";
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    continue;
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var display = ReadDisplay(json) ?? code;
                return Build(kind, code, sys, display, incomplete: kind == GenerationCatalogKind.Observation);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Terminology lookup failed for {System}|{Code}.", sys, code);
            }
        }

        return Fallback(kind, code, guessed);
    }

    private static GenerationCatalogItem Fallback(GenerationCatalogKind kind, string code, string system)
        => Build(kind, code, system, code, incomplete: true);

    private static GenerationCatalogItem Build(
        GenerationCatalogKind kind,
        string code,
        string system,
        string display,
        bool incomplete)
        => new()
        {
            Kind = kind,
            System = system,
            Code = code,
            Display = display,
            Category = kind == GenerationCatalogKind.Observation ? "laboratory" : null,
            IsLab = kind == GenerationCatalogKind.ServiceRequest
                && string.Equals(system, GenerationCatalogItem.Loinc, StringComparison.OrdinalIgnoreCase),
            Incomplete = incomplete,
            IsSeed = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private static IEnumerable<string> DistinctSystems(GenerationCatalogKind kind, string guessed)
    {
        var list = new List<string> { guessed };
        if (kind is GenerationCatalogKind.Observation or GenerationCatalogKind.ServiceRequest)
            list.Add(GenerationCatalogItem.Loinc);
        if (kind == GenerationCatalogKind.Medication)
            list.Add(GenerationCatalogItem.RxNorm);
        if (kind is GenerationCatalogKind.Condition or GenerationCatalogKind.Procedure)
            list.Add(GenerationCatalogItem.Snomed);
        return list.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadDisplay(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("parameter", out var parameters)
                || parameters.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var p in parameters.EnumerateArray())
            {
                var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!string.Equals(name, "display", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (p.TryGetProperty("valueString", out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
