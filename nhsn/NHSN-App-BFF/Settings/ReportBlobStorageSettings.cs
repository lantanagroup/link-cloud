namespace LantanaGroup.Link.Nhsn.App.Bff.Settings;

// Read-only credentials to the same Azure Storage account Report's own BlobStorageSettings writes
// patient report blobs to -- see IReportBlobStorageClient for why the BFF can't just GET the bare
// blob URI Report records.
public class ReportBlobStorageSettings
{
    public const string SectionName = "ReportBlobStorage";

    public string? ConnectionString { get; set; }
}
