using LantanaGroup.Link.MockDmrpApi.Contracts.Generated;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;

namespace LantanaGroup.Link.MockDmrpApi.Application.Mapping;

/// <summary>
/// Projects stored entries into the generated contract type.
/// </summary>
/// <remarks>
/// This is the seam that keeps the third-party contract out of the database. Everything
/// below it deals in <see cref="ReportingPlanEntryEntity"/>; only this file and the two
/// contract endpoints deal in generated types. When Contracts/dmrp-openapi.yaml is replaced
/// -- which is expected, the current one being provisional -- the compile errors land here
/// rather than in the service layer or a migration.
/// <para>
/// The support surface does not pass through here. It has its own models, because it is ours
/// and has no reason to move when the third party's contract does.
/// </para>
/// </remarks>
public static class EntryMapper
{
    /// <summary>
    /// Projects a facility's entries for one period into a reporting plan.
    /// </summary>
    /// <param name="facilityId">The facility the plan belongs to.</param>
    /// <param name="reportingMonth">
    /// The month for a monthly plan, or <c>null</c> for an annual one, where a month has no
    /// meaning and the response omits it.
    /// </param>
    /// <param name="reportingYear">The reporting year.</param>
    /// <param name="entries">The entries the facility is enrolled in for that period.</param>
    /// <param name="retrievedOn">When the response was produced.</param>
    /// <remarks>
    /// Only the supplied entries appear in <c>measures</c>. A measure the facility is not
    /// enrolled in is simply absent -- there is no negative representation -- so an empty
    /// collection produces an empty measures array rather than an error or a null.
    /// </remarks>
    public static ReportingPlanResponse ToReportingPlan(
        string facilityId,
        int? reportingMonth,
        int reportingYear,
        IReadOnlyList<ReportingPlanEntryEntity> entries,
        DateTimeOffset retrievedOn)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new ReportingPlanResponse
        {
            FacilityId = facilityId,
            ReportingMonth = reportingMonth,
            ReportingYear = reportingYear,
            Measures = entries
                .Select(e => new ReportingPlanMeasure
                {
                    Measure = e.Measure,
                    IsReporting = e.IsReporting
                })
                .ToList(),
            RetrievedOn = retrievedOn
        };
    }
}
