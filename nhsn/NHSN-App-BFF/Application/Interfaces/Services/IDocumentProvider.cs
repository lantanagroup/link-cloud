namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;

public enum DocumentStatus
{
    Ok,
    NotFound,
    DirectoryUnavailable
}

public sealed record DocumentResult(DocumentStatus Status, byte[]? Content = null, string? FileName = null, string? ContentType = null);

// Serves the vendor instruction documents named by VendorProfile.DocumentKeys. Keys, never
// filenames — resolved against a fixed allow-list built from the vendor catalog so a caller can
// never use this to read an arbitrary file off disk.
public interface IDocumentProvider
{
    Task<DocumentResult> GetAsync(string documentKey, CancellationToken cancellationToken = default);
}
