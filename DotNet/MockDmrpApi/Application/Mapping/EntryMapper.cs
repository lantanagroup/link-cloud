using System.Globalization;
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
    /// Projects a facility's entries into a reporting plan.
    /// </summary>
    /// <param name="nhsnOrgId">The facility the plan belongs to.</param>
    /// <param name="reportingMonth">
    /// The month the result was narrowed to, echoed from the request, or <c>null</c> when none
    /// was supplied.
    /// </param>
    /// <param name="reportingYear">
    /// The year the result was narrowed to, or <c>null</c> when none was supplied -- in which
    /// case the entries may span several years.
    /// </param>
    /// <param name="entries">The entries the facility is enrolled in.</param>
    /// <param name="retrievedOn">When the response was produced.</param>
    /// <remarks>
    /// Only the supplied entries appear in <c>measures</c>. A module the facility is not
    /// enrolled in is simply absent -- there is no negative representation -- so an empty
    /// collection produces an empty measures array rather than an error or a null.
    /// <para>
    /// The month and year are echoed rather than derived from the entries. They describe what
    /// the caller asked for, which is the only honest answer when no period was supplied and
    /// the result spans several.
    /// </para>
    /// </remarks>
    public static ReportingPlanResponse ToReportingPlan(
        string nhsnOrgId,
        int? reportingMonth,
        int? reportingYear,
        IReadOnlyList<ReportingPlanEntryEntity> entries,
        DateTimeOffset retrievedOn)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return new ReportingPlanResponse
        {
            PsDMRptPlanID = PlanIdentifier(nhsnOrgId, reportingMonth, reportingYear),

            // Numeric at the root, a string inside plans. Both come from the same
            // facility identifier; only the type differs. See the note below.
            Orgid = int.TryParse(nhsnOrgId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orgId)
                ? orgId
                : null,
            Year = reportingYear,
            Month = reportingMonth,

            CreateDate = FormatTimestamp(entries.Count == 0
                ? retrievedOn.UtcDateTime
                : entries.Min(e => e.CreateDate)),
            ModifyDate = FormatTimestamp(entries.Count == 0
                ? retrievedOn.UtcDateTime
                : entries.Max(e => e.ModifyDate ?? e.CreateDate)),

            Plans = entries
                .Select(e => new ReportingPlanItem
                {
                    Name = e.Measure,
                    Nhsnorgid = nhsnOrgId,
                    Month = e.ReportingMonth,
                    Year = e.ReportingYear,
                    Reporting = e.IsReporting,
                    RptSeq = 0
                })
                .ToList()
        };
    }

    /// <summary>
    /// The timestamp format the real API uses: a space separator, two fractional
    /// digits and no timezone.
    /// </summary>
    /// <remarks>
    /// Formatted by hand, and typed as a string in the contract, so it is emitted exactly as
    /// received rather than normalised to ISO 8601. Binding it as a date would produce
    /// <c>2023-09-09T11:12:12.59+00:00</c> — well-formed, and not what a consumer will have to
    /// parse in production.
    /// </remarks>
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.ff";

    private static string FormatTimestamp(DateTime value) =>
        value.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// A stable identifier for the plan a query describes.
    /// </summary>
    /// <remarks>
    /// The real API returns a stored record's key. Nothing here stores plans — they are
    /// assembled per request — so this derives one from what identifies the plan, which keeps
    /// it stable across repeated identical queries. A counter or a random value would change
    /// under a caller that reasonably expects it not to.
    /// </remarks>
    private static int PlanIdentifier(string nhsnOrgId, int? month, int? year)
    {
        var key = $"{nhsnOrgId}|{year?.ToString(CultureInfo.InvariantCulture) ?? "-"}"
                  + $"|{month?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

        // FNV-1a rather than string.GetHashCode(), which is randomised per process in .NET
        // Core -- the same query would return a different identifier after every restart,
        // which is the one thing this is supposed not to do.
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            var hash = offsetBasis;
            foreach (var c in key)
            {
                hash = (hash ^ c) * prime;
            }

            // Masked to 31 bits so it is always non-negative and reads like a record id.
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
