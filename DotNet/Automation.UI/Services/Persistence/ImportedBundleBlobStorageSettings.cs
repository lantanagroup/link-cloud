namespace Automation.UI.Services.Persistence;

public sealed class ImportedBundleBlobStorageSettings
{
    public const string Key = "InternalBlobStorage";

    public string? ConnectionString { get; set; }
    public string? BlobContainerName { get; set; }
    public string? BlobRoot { get; set; }
    public string? GeneratedTemplateBlobRoot { get; set; }
}
