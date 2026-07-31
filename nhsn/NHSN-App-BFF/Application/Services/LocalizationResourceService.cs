using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services;

public sealed class LocalizationResourceService : ILocalizationResourceService
{
    private const string DefaultLocale = "en-US";
    private static readonly HashSet<string> AllowedNamespaces = ["common", "onboarding", "configuration"];

    private readonly IOptionsMonitor<LocalizationSettings> _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<LocalizationResourceService> _logger;

    public LocalizationResourceService(
        IOptionsMonitor<LocalizationSettings> settings,
        IWebHostEnvironment environment,
        ILogger<LocalizationResourceService> logger)
    {
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    public async Task<LocalizationResourceResult> GetNamespaceAsync(string locale, string namespaceName, CancellationToken cancellationToken)
    {
        if (!IsValidLocale(locale))
        {
            return new LocalizationResourceResult(LocalizationResourceStatus.InvalidLocale, Message: "The requested locale is not valid.");
        }

        if (!AllowedNamespaces.Contains(namespaceName))
        {
            return new LocalizationResourceResult(LocalizationResourceStatus.InvalidNamespace, Message: "The requested namespace is not supported.");
        }

        var resourceDirectory = ResolveResourceDirectory(_settings.CurrentValue.ResourceDirectory);
        if (resourceDirectory is null || !Directory.Exists(resourceDirectory))
        {
            _logger.LogError("Localization resource directory was not found or unavailable. Path={Path}", resourceDirectory ?? "<null>");
            return new LocalizationResourceResult(LocalizationResourceStatus.DirectoryUnavailable, Message: "Localization resource directory is unavailable.");
        }

        var localeChain = BuildLocaleFallbackChain(locale);
        var resourceByLocale = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var resolvedFiles = new List<string>();

        foreach (var candidateLocale in localeChain)
        {
            var filePath = BuildFilePath(resourceDirectory, candidateLocale, namespaceName);
            if (filePath is null || !File.Exists(filePath))
            {
                _logger.LogDebug("Localization resource not found for locale/namespace. Locale={Locale}; Namespace={Namespace}", candidateLocale, namespaceName);
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var rootNode = JsonNode.Parse(json) as JsonObject;
                if (rootNode is null)
                {
                    _logger.LogWarning("Localization resource file does not contain a JSON object. File={File}", filePath);
                    return new LocalizationResourceResult(LocalizationResourceStatus.MalformedJson, Message: "Localization JSON payload must be an object.");
                }

                resourceByLocale[candidateLocale] = rootNode;
                resolvedFiles.Add(filePath);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Localization resource JSON is malformed. File={File}", filePath);
                return new LocalizationResourceResult(LocalizationResourceStatus.MalformedJson, Message: "Localization JSON payload is malformed.");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Localization resource file could not be read. File={File}", filePath);
                return new LocalizationResourceResult(LocalizationResourceStatus.DirectoryUnavailable, Message: "Localization resource file could not be read.");
            }
        }

        if (!resourceByLocale.ContainsKey(DefaultLocale))
        {
            _logger.LogError("Default English localization was not found for namespace. Namespace={Namespace}", namespaceName);
            return new LocalizationResourceResult(LocalizationResourceStatus.NotFound, Message: "Default localization resources are unavailable.");
        }

        var merged = new JsonObject();
        foreach (var candidateLocale in localeChain.Reverse())
        {
            if (resourceByLocale.TryGetValue(candidateLocale, out var candidateNode))
            {
                MergeObjects(merged, candidateNode);
            }
        }

        var payload = merged.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var lastModified = resolvedFiles
            .Select(path => new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero))
            .Max();
        var eTag = ComputeETag(payload, lastModified);

        return new LocalizationResourceResult(LocalizationResourceStatus.Ok, payload, eTag, lastModified);
    }

    private string? ResolveResourceDirectory(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "Localization";
        }

        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
    }

    private static bool IsValidLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return false;
        }

        if (locale.Contains("..", StringComparison.Ordinal) || locale.Contains('/') || locale.Contains('\\'))
        {
            return false;
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(locale);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> BuildLocaleFallbackChain(string locale)
    {
        var locales = new List<string>();
        if (!string.IsNullOrWhiteSpace(locale))
        {
            locales.Add(locale);

            var separatorIndex = locale.IndexOf('-');
            if (separatorIndex > 0)
            {
                locales.Add(locale[..separatorIndex]);
            }
        }

        if (!locales.Contains(DefaultLocale, StringComparer.OrdinalIgnoreCase))
        {
            locales.Add(DefaultLocale);
        }

        return locales.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? BuildFilePath(string root, string locale, string namespaceName)
    {
        var filePath = Path.GetFullPath(Path.Combine(root, locale, $"{namespaceName}.json"));
        var rootPath = Path.GetFullPath(root);
        if (!filePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return filePath;
    }

    private static void MergeObjects(JsonObject destination, JsonObject source)
    {
        foreach (var item in source)
        {
            if (item.Value is JsonObject sourceObject)
            {
                if (destination[item.Key] is JsonObject destinationObject)
                {
                    MergeObjects(destinationObject, sourceObject);
                }
                else
                {
                    destination[item.Key] = sourceObject.DeepClone();
                }
            }
            else
            {
                destination[item.Key] = item.Value?.DeepClone();
            }
        }
    }

    private static string ComputeETag(string payload, DateTimeOffset lastModified)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{payload}|{lastModified.ToUnixTimeMilliseconds()}");
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}