using System.IO.Compression;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
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
    private readonly IFacilityGateway _facilityGateway;
    private readonly INhsnUserContext _userContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PackageZipDownloadService> _logger;

    public PackageZipDownloadService(
        IOnboardingReadService readService,
        IFacilityGateway facilityGateway,
        INhsnUserContext userContext,
        IWebHostEnvironment environment,
        ILogger<PackageZipDownloadService> logger)
    {
        _readService = readService;
        _facilityGateway = facilityGateway;
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

        // Only for the "Facility: ..." / "Vendor: ..." lines at the top of the import sheet - every
        // other file, and every other cell of the sheet itself, is still copied byte for byte.
        // Without this, every facility's sheet named whichever sample facility happened to be on
        // disk when the asset was authored, not the facility that actually downloaded it.
        var facility = await _facilityGateway.GetAsync(facilityId, cancellationToken);
        var facilityLine = $"Facility: {facility?.FacilityName ?? facilityId} ({facilityId})";
        var vendorLine = $"Vendor: {vendor.Value}";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, filePath) in resolved)
            {
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();

                if (Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    var templateBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                    var personalized = ManualUploadTemplatePersonalizer.Personalize(templateBytes, facilityLine, vendorLine);
                    await entryStream.WriteAsync(personalized, cancellationToken);
                }
                else
                {
                    // Every other file is copied verbatim — extracting it from the zip yields the
                    // same bytes as the file on disk.
                    await using var source = File.OpenRead(filePath);
                    await source.CopyToAsync(entryStream, cancellationToken);
                }
            }
        }

        return new PackageZipDownloadResult(
            PackageZipDownloadStatus.Ok,
            stream.ToArray(),
            $"{facilityId}_import_sheet.zip");
    }
}
