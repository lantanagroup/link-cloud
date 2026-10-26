namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// The real bytes Report generated and stored for one patient's report, for the patient report
// download action -- Content-Type/FileName mirror what Report's BlobStorageService set at upload.
public sealed record PatientReportDownload
{
    public required byte[] Content { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}
