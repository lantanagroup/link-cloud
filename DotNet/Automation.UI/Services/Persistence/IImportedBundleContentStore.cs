namespace Automation.UI.Services.Persistence;

public sealed record StoredImportedBundleContent(string BlobName, long ByteCount);

public interface IImportedBundleContentStore
{
    Task<StoredImportedBundleContent> StoreAsync(Guid bundleId, string contentHash, string bundleJson, CancellationToken ct = default);
    Task<string?> ReadAsync(ImportedBundleDocument bundle, CancellationToken ct = default);
    Task DeleteAsync(ImportedBundleDocument bundle, CancellationToken ct = default);
}
