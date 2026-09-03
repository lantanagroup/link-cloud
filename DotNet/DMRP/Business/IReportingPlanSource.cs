using LantanaGroup.Link.Shared.Application.Models;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// One measure a facility is enrolled to report, already resolved through its measure mapping to
    /// the digital quality measure Link evaluates patients against.
    /// </summary>
    /// <param name="Measure">The NHSN measure (module) the facility enrolled in, such as HOB.</param>
    /// <param name="DQM">The dQM the measure maps to, or empty when Link has no mapping for it.</param>
    /// <param name="Frequency">
    /// How often the dQM is reported, or null when Link has no mapping for the measure. The cadence
    /// is the mapping's, so an enrollment waiting to be mapped has none -- naming one would put a
    /// cadence nobody chose on the facility's look-ahead.
    /// </param>
    public sealed record ReportingPlanEntry(string Measure, string DQM, Frequency? Frequency);

    /// <summary>
    /// Where a facility's reporting plan for a period comes from.
    /// </summary>
    /// <remarks>
    /// The implementation registered today reads the reporting plans already stored in the module's
    /// own tables. When the DMRP API client lands, an implementation that refreshes those rows from
    /// the API before returning them takes its place, and nothing that consumes this interface has to
    /// change.
    /// </remarks>
    public interface IReportingPlanSource
    {
        /// <summary>
        /// The measures the facility is enrolled to report in the given period. A facility enrolled in
        /// nothing returns an empty list, which is a meaningful answer rather than a missing one.
        /// </summary>
        Task<IReadOnlyList<ReportingPlanEntry>> GetForPeriodAsync(string facilityId, int month, int year,
            CancellationToken cancellationToken = default);
    }
}
