namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Reporting;

// The blob storage location of one patient's generated report for a report type, resolved from
// Report's own entry record, for the patient report download action.
public sealed record PatientReportBlobReference
{
    public required string Uri { get; init; }

    public required string FileName { get; init; }
}
