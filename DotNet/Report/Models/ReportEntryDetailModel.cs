using LantanaGroup.Link.Report.Domain.Models;

namespace LantanaGroup.Link.Report.Models;

/// <summary>
/// One patient's report entry with the evidence behind its mapping indicators.
/// </summary>
/// <remarks>
/// The drill-down behind a cell in the report detail grid. The grid itself carries only the indicators --
/// serializing the evidence for every row of every page would be work the table never reads -- so the
/// counts and the offending codes live here instead.
/// </remarks>
public class ReportEntryDetailModel : ReportEntryModel
{
    /// <summary>
    /// What DataAcquisition found when it resolved the patient's encounters, or null if it never reported.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty object on purpose: an empty object would claim acquisition ran and found
    /// nothing, which is a different fact from acquisition not having answered.
    /// </remarks>
    public AcquisitionMappingDetails? Acquisition { get; set; }

    /// <summary>
    /// What Normalization counted per code map, or null if it never reported.
    /// </summary>
    /// <inheritdoc cref="Acquisition" path="/remarks"/>
    public NormalizationMappingDetails? Normalization { get; set; }
}
