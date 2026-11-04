using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.VendorProfiles;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Session;

// The allow-list is every DocumentKeys value across every vendor profile — closed over the
// catalog, not configuration, since a key that isn't wired to a profile has nothing to link to.
public sealed class DocumentProvider : IDocumentProvider
{
    private static readonly HashSet<string> AllowedKeys = VendorProfileCatalog.All
        .SelectMany(profile => new[]
        {
            profile.DocumentKeys.CensusInstructions,
            profile.DocumentKeys.JwksInstructions,
            profile.DocumentKeys.LocationOrgResolution
        })
        .Where(key => !string.IsNullOrWhiteSpace(key))
        .Select(key => key!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly IOptionsMonitor<DocumentSettings> _settings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DocumentProvider> _logger;

    public DocumentProvider(IOptionsMonitor<DocumentSettings> settings, IWebHostEnvironment environment, ILogger<DocumentProvider> logger)
    {
        _settings = settings;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DocumentResult> GetAsync(string documentKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentKey) || !AllowedKeys.Contains(documentKey))
        {
            return new DocumentResult(DocumentStatus.NotFound);
        }

        var resourceDirectory = ResolveResourceDirectory(_settings.CurrentValue.ResourceDirectory);
        if (!Directory.Exists(resourceDirectory))
        {
            _logger.LogError("Document resource directory was not found. Path={Path}", resourceDirectory);
            return new DocumentResult(DocumentStatus.DirectoryUnavailable);
        }

        // documentKey is validated against the allow-list above, so it never reaches the file
        // system as untrusted input — but the file name is still built the same guarded way the
        // localization provider builds its path, rather than trusted implicitly.
        var filePath = Path.GetFullPath(Path.Combine(resourceDirectory, $"{documentKey}.pdf"));
        var rootPath = Path.GetFullPath(resourceDirectory);
        if (!filePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
        {
            return new DocumentResult(DocumentStatus.NotFound);
        }

        var content = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return new DocumentResult(DocumentStatus.Ok, content, Path.GetFileName(filePath), "application/pdf");
    }

    private string ResolveResourceDirectory(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "Documents";
        }

        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, configuredPath));
    }
}
