using Hl7.Fhir.Model;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// One hour's upload: everything that happened in [Hour, Hour + 1h) plus the census lists as they stand
/// at the end of that hour.
/// </summary>
public sealed record HourlyFile(DateTimeOffset Hour, Bundle Bundle)
{
    /// <summary>
    /// File name the hour is stored under, e.g. 2026-11-03T14Z.json.
    /// </summary>
    public string FileName => MonthWriter.FileName(Hour);

    /// <summary>
    /// Number of resources in the bundle.
    /// </summary>
    public int EntryCount => Bundle.Entry.Count;
}
