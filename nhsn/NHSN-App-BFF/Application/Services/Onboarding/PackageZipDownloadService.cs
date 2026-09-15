using System.IO.Compression;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

public sealed class PackageZipDownloadService : IPackageZipDownloadService
{
    private const string StaticAssetsDirectory = "StaticAssets";

    // Paths relative to StaticAssets, case-exact because the deployment target is case-sensitive.
    //
    // Kept here rather than on VendorProfile: the profile is serialized to the browser by
    // GET /reference/vendors, and the on-disk layout of the server's own asset folder is not part
    // of that contract. The UI names no file — it asks for the package and gets a zip.
    private static readonly Dictionary<EhrVendor, string[]> PackageFilesByVendor = new()
    {
        [EhrVendor.Epic] =
        [
            "excel-sheets/Epic_Import_Sheet.xlsx",
            "census-instructions/Epic_Census_Instructions.pdf",
            "jwks-instructions/Epic_JWKS_Instructions.pdf",

            // Epic only — Cerner's location and organization resolution needs no separate guide.
            "location-org-resolution/Location_Org_Resolution.pdf"
        ],
        [EhrVendor.Cerner] =
        [
            "excel-sheets/Cerner_Import_Sheet.xlsx",
            "census-instructions/Cerner_Census_Instructions.pdf",
            "jwks-instructions/Cerner_JWKS_Instructions.pdf"
        ]
    };

    private readonly IOnboardingReadService _readService;
    private readonly INhsnUserContext _userContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PackageZipDownloadService> _logger;

    public PackageZipDownloadService(
        IOnboardingReadService readService,
        INhsnUserContext userContext,
        IWebHostEnvironment environment,
        ILogger<PackageZipDownloadService> logger)
    {
        _readService = readService;
        _userContext = userContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task<PackageZipDownloadResult> ExportAsync(CancellationToken cancellationToken = default)
    {
        var facilityId = _userContext.RequireFacilityId();

        var envelope = await _readService.GetAsync(cancellationToken);
        var vendor = envelope.Draft?.FacilityInfo.Vendor;

        if (vendor is null || !PackageFilesByVendor.TryGetValue(vendor.Value, out var relativePaths))
        {
            return new PackageZipDownloadResult(PackageZipDownloadStatus.VendorNotSelected);
        }

        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, StaticAssetsDirectory));

        var resolved = new List<(string EntryName, string FilePath)>();
        var missing = new List<string>();
        foreach (var relativePath in relativePaths)
        {
            var filePath = Path.GetFullPath(Path.Combine(root, relativePath));
            if (File.Exists(filePath))
            {
                // Flat inside the zip: the folders exist to organize the server's assets, and a
                // facility opening the bundle wants four files, not four folders holding one each.
                resolved.Add((Path.GetFileName(filePath), filePath));
            }
            else
            {
                missing.Add(relativePath);
            }
        }

        if (missing.Count > 0)
        {
            _logger.LogError(
                "Manual-upload package assets are missing for vendor {Vendor}. Root={Root} Missing={Missing}",
                vendor,
                root,
                string.Join(", ", missing));
            return new PackageZipDownloadResult(PackageZipDownloadStatus.AssetsUnavailable, MissingFiles: missing);
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, filePath) in resolved)
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                // Copied verbatim — no reading, no rewriting. Extracting the zip yields the same
                // bytes as the file on disk.
                await using var entryStream = entry.Open();
                await using var source = File.OpenRead(filePath);
                await source.CopyToAsync(entryStream, cancellationToken);
            }
        }

        return new PackageZipDownloadResult(
            PackageZipDownloadStatus.Ok,
            stream.ToArray(),
            $"{facilityId}_import_sheet.zip");
    }
}
