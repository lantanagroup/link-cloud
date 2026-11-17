using LantanaGroup.Automation.Configuration;
using LantanaGroup.Link.Automation.Link.Configuration;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// One named FHIR endpoint: the base URL Link.UI calls, the base URL a facility's
/// services call, the auth for that endpoint, and an optional facility template.
/// The two URLs are the same server seen from two network vantages. This is not a vendor.
/// </summary>
public sealed record NamedEhrSystem(
    string Name,
    string FhirServerBase,
    string FacilityFhirServerBase,
    string? DefaultFacilityTemplate,
    bool IsDefault,
    OAuthConfig OAuth,
    BasicAuthConfig BasicAuth);

/// <summary>
/// Configuration shape bound from <c>Automation:EhrSystems</c>. Secrets stay empty
/// in committed files and are supplied by App Configuration or environment variables.
/// </summary>
public sealed class EhrSystemEntry
{
    public string Name { get; set; } = string.Empty;
    public string FhirServerBase { get; set; } = string.Empty;
    public string? FacilityFhirServerBase { get; set; }
    public string? DefaultFacilityTemplate { get; set; }
    public bool IsDefault { get; set; }
    public OAuthConfig OAuth { get; set; } = new();
    public BasicAuthConfig BasicAuth { get; set; } = new();
}

/// <summary>
/// The named EHR systems for this process. An empty <c>Automation:EhrSystems</c>
/// list becomes one system taken from the single FHIR base and auth settings.
/// </summary>
public sealed class EhrSystemCatalog
{
    public const string LegacyName = "default";
    public const string SectionPath = "Automation:EhrSystems";

    public EhrSystemCatalog(IConfiguration configuration, IOptionsMonitor<AutomationConfig> automation)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(automation);
        Systems = Parse(configuration, automation.CurrentValue);
    }

    public IReadOnlyList<NamedEhrSystem> Systems { get; }

    public NamedEhrSystem Default => SelectDefault(Systems);

    public NamedEhrSystem? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var trimmed = name.Trim();
        return Systems.FirstOrDefault(system =>
            string.Equals(system.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public NamedEhrSystem Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Default;

        return Find(name)
            ?? throw new InvalidOperationException(
                $"EHR system '{name.Trim()}' is not configured. Configured systems: {ConfiguredNames()}.");
    }

    /// <summary>
    /// Copies the named endpoint onto a new config for one run. Loki, query, and
    /// generation settings stay on the host config. The host instance is left unchanged.
    /// </summary>
    public AutomationConfig Overlay(AutomationConfig host, string? ehrSystemName)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (string.IsNullOrWhiteSpace(ehrSystemName))
            return host;

        var system = Find(ehrSystemName)
            ?? throw new InvalidOperationException(
                $"EHR system '{ehrSystemName.Trim()}' is not configured. Configured systems: {ConfiguredNames()}.");
        return WithEndpoints(host, system);
    }

    public static IReadOnlyList<NamedEhrSystem> Parse(IConfiguration configuration, AutomationConfig legacy)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(legacy);

        var entries = new List<EhrSystemEntry>();
        configuration.GetSection(SectionPath).Bind(entries);
        if (entries.Count == 0)
            return [FromLegacy(legacy)];

        return Validate(entries);
    }

    public static IReadOnlyList<NamedEhrSystem> Validate(IReadOnlyList<EhrSystemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            throw new InvalidOperationException("Automation:EhrSystems is empty.");

        var systems = new List<NamedEhrSystem>(entries.Count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var name = RequireName(entry.Name);
            if (!names.Add(name))
                throw new InvalidOperationException($"EHR system '{name}' is listed more than once.");

            var consumerBase = RequireHttpUrl(entry.FhirServerBase, name, "FhirServerBase");
            var facilityBase = string.IsNullOrWhiteSpace(entry.FacilityFhirServerBase)
                ? consumerBase
                : RequireHttpUrl(entry.FacilityFhirServerBase, name, "FacilityFhirServerBase");
            var template = CleanTemplate(entry.DefaultFacilityTemplate, name);
            var oauth = CopyOAuth(entry.OAuth);
            var basic = CopyBasic(entry.BasicAuth);
            if (oauth.ShouldAuthenticate && basic.ShouldAuthenticate)
            {
                throw new InvalidOperationException(
                    $"EHR system '{name}' cannot enable OAuth and basic auth together.");
            }

            if (oauth.ShouldAuthenticate)
                RequireOAuth(oauth, name);
            if (basic.ShouldAuthenticate)
                RequireBasic(basic, name);

            systems.Add(new NamedEhrSystem(name, consumerBase, facilityBase, template, entry.IsDefault, oauth, basic));
        }

        _ = SelectDefault(systems);
        return systems;
    }

    public static NamedEhrSystem SelectDefault(IReadOnlyList<NamedEhrSystem> systems)
    {
        ArgumentNullException.ThrowIfNull(systems);
        var marked = systems.Where(system => system.IsDefault).ToList();
        if (marked.Count == 1)
            return marked[0];
        if (marked.Count == 0 && systems.Count == 1)
            return systems[0];
        if (marked.Count == 0)
        {
            throw new InvalidOperationException(
                "Name one EHR system as the default when more than one is configured.");
        }

        throw new InvalidOperationException("Only one EHR system can be the default.");
    }

    public static AutomationConfig WithEndpoints(AutomationConfig host, NamedEhrSystem system)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(system);
        return new AutomationConfig
        {
            FhirServerBase = system.FhirServerBase,
            FacilityFhirServerBase = system.FacilityFhirServerBase,
            FhirServerOAuth = CopyOAuth(system.OAuth),
            FhirServerBasicAuth = CopyBasic(system.BasicAuth),
            LokiBaseUrl = host.LokiBaseUrl,
            LokiAppLabel = host.LokiAppLabel,
            DownloadPath = host.DownloadPath,
            FhirQuery = host.FhirQuery,
            FhirGeneration = host.FhirGeneration,
            Kafka = host.Kafka
        };
    }

    private string ConfiguredNames() => string.Join(", ", Systems.Select(system => system.Name));

    private static NamedEhrSystem FromLegacy(AutomationConfig legacy)
    {
        var consumer = string.IsNullOrWhiteSpace(legacy.FhirServerBase)
            ? new AutomationConfig().FhirServerBase
            : legacy.FhirServerBase.Trim();
        var facility = string.IsNullOrWhiteSpace(legacy.FacilityFhirServerBase)
            ? consumer
            : legacy.FacilityFhirServerBase.Trim();
        return new NamedEhrSystem(
            LegacyName,
            consumer,
            facility,
            DefaultFacilityTemplate: null,
            IsDefault: true,
            CopyOAuth(legacy.FhirServerOAuth),
            CopyBasic(legacy.FhirServerBasicAuth));
    }

    private static string RequireName(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 80 || trimmed.Any(char.IsControl))
            throw new InvalidOperationException("Each EHR system needs a name of 1 to 80 characters.");

        return trimmed;
    }

    private static string RequireHttpUrl(string? value, string systemName, string field)
    {
        var trimmed = (value ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                $"EHR system '{systemName}' {field} must be an absolute http or https URL.");
        }

        return trimmed;
    }

    private static string? CleanTemplate(string? value, string systemName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > 200 || trimmed.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"EHR system '{systemName}' has a default facility template that cannot be used.");
        }

        return trimmed;
    }

    private static void RequireOAuth(OAuthConfig oauth, string systemName)
    {
        RequireAuthField(oauth.TokenEndpoint, systemName, "OAuth token endpoint");
        RequireAuthField(oauth.ClientId, systemName, "OAuth client id");
        RequireAuthField(oauth.Username, systemName, "OAuth username");
        RequireAuthField(oauth.Password, systemName, "OAuth password");
    }

    private static void RequireBasic(BasicAuthConfig basic, string systemName)
    {
        RequireAuthField(basic.Username, systemName, "basic auth username");
        RequireAuthField(basic.Password, systemName, "basic auth password");
    }

    private static void RequireAuthField(string? value, string systemName, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"EHR system '{systemName}' needs a {field} when that auth mode is on.");
        }
    }

    private static OAuthConfig CopyOAuth(OAuthConfig? source) => new()
    {
        ShouldAuthenticate = source?.ShouldAuthenticate ?? false,
        TokenEndpoint = source?.TokenEndpoint,
        ClientId = source?.ClientId,
        ClientSecret = source?.ClientSecret,
        Scope = string.IsNullOrWhiteSpace(source?.Scope) ? "openid profile email" : source.Scope,
        Username = source?.Username,
        Password = source?.Password
    };

    private static BasicAuthConfig CopyBasic(BasicAuthConfig? source) => new()
    {
        ShouldAuthenticate = source?.ShouldAuthenticate ?? false,
        Username = source?.Username,
        Password = source?.Password
    };
}
