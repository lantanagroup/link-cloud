namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public enum PackageZipDownloadStatus
{
    Ok,

    // The facility has not chosen an EHR vendor yet, so there is no package to build — the bundle
    // is vendor-specific from the first file.
    VendorNotSelected,

    // One or more of the vendor's files is missing from the deployed StaticAssets directory. A
    // deployment fault, not a facility one: served as a 503 rather than a partial zip, because a
    // bundle silently missing the census instructions is worse than no bundle.
    AssetsUnavailable
}

public sealed record PackageZipDownloadResult(
    PackageZipDownloadStatus Status,
    byte[]? Content = null,
    string? FileName = null,
    IReadOnlyList<string>? MissingFiles = null);

// Builds the manual-upload import package: the vendor's import sheet, its census and JWKS
// instructions, and — for Epic only — the org-resolution guidance, zipped together.
//
// Every file is copied byte for byte out of StaticAssets, except the import sheet itself: its
// "Facility: ..." / "Vendor: ..." header lines are rewritten to name the actual downloading
// facility (see ManualUploadTemplatePersonalizer) before it's added to the zip. Nothing else in
// the sheet, and nothing in any other file, is touched.
public interface IPackageZipDownloadService
{
    Task<PackageZipDownloadResult> ExportAsync(CancellationToken cancellationToken = default);
}
