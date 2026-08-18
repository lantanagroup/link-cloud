namespace Automation.UI.Services.Persistence;

public sealed class ImportedBundleBlobStorageSettings
{
    public const string Key = "InternalBlobStorage";

    public string? ConnectionString { get; set; }
    public string? BlobContainerName { get; set; }
    public string? BlobRoot { get; set; }
    public string? GeneratedTemplateBlobRoot { get; set; }

    // Snapshot externalization settings (Automation.UI domain snapshots -> ABS).
    // Defaults keep behavior safe without requiring extra config.
    public string? SnapshotPayloadBlobRoot { get; set; }
    public int SnapshotPayloadInlineMaxBytes { get; set; } = 256 * 1024;
    public List<string> SnapshotPayloadExternalizedDomains { get; set; } =
    [
        "generationManifest",
        "entries",
        "measureResources",
        "absUpload"
    ];
}
