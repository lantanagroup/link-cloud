using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;

namespace LantanaGroup.Link.MockDmrpApi.Application.Services;

/// <summary>
/// Reporting plan entry storage. Returns domain entities; translation to the generated
/// contract types happens at the controller boundary so that a revision of
/// Contracts/dmrp-openapi.yaml never reaches this layer or the database.
/// </summary>
public interface IReportingPlanService
{
    Task<ReportingPlanEntryEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ReportingPlanEntryEntity> Records, PaginationMetadata Metadata)> GetByFacilityAsync(
        string facilityId, int pageSize, int pageNumber, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ReportingPlanEntryEntity> Records, PaginationMetadata Metadata)> SearchAsync(
        ReportingPlanSearchCriteria criteria, CancellationToken cancellationToken);

    /// <summary>
    /// The entries making up a facility's reporting plan for one component.
    /// </summary>
    /// <param name="component">MSC or PS. Always applied — the two plans never mix.</param>
    /// <param name="nhsnOrgId">The facility. Always applied.</param>
    /// <param name="measure">An NHSN module to narrow to, or null for every module.</param>
    /// <param name="reportingMonth">A month to narrow to, or null for every month.</param>
    /// <param name="reportingYear">A year to narrow to, or null for every year.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <remarks>
    /// Component and facility are the only filters always applied; the rest narrow the result
    /// when supplied and are ignored when not. A caller passing neither month nor year gets
    /// the facility's whole plan for that component.
    /// <para>
    /// Only entries actively being reported are returned; the absence of a module is what
    /// conveys "not enrolled".
    /// </para>
    /// <para>
    /// Annual components carry no reporting month, so the caller passes null for
    /// <paramref name="reportingMonth"/> there. Matching on a month would exclude every row
    /// it is supposed to return.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<ReportingPlanEntryEntity>> GetReportingPlanAsync(
        string component,
        string nhsnOrgId,
        string? measure,
        int? reportingMonth,
        int? reportingYear,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <exception cref="InvalidReportingPlanEntryException">
    /// The component is unknown, or the reporting month does not match the component's cadence.
    /// </exception>
    /// <exception cref="DuplicateReportingPlanEntryException">
    /// An entry already exists for the same facility, component, measure and period.
    /// </exception>
    Task<ReportingPlanEntryEntity> CreateAsync(ReportingPlanEntryEntity entry, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing entry. Returns <c>null</c> when no entry has the supplied
    /// identifier -- this never creates one.
    /// </summary>
    /// <exception cref="InvalidReportingPlanEntryException">
    /// The component is unknown, or the reporting month does not match the component's cadence.
    /// </exception>
    /// <exception cref="DuplicateReportingPlanEntryException">
    /// The update would collide with another entry's facility, component, measure and period.
    /// </exception>
    Task<ReportingPlanEntryEntity?> UpdateAsync(ReportingPlanEntryEntity entry, CancellationToken cancellationToken);

    /// <summary>Returns false when no entry had the supplied identifier.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>Returns the number of entries removed, which may be zero.</summary>
    Task<int> DeleteByFacilityAsync(string facilityId, CancellationToken cancellationToken);

    /// <summary>Returns the number of entries removed, which may be zero.</summary>
    Task<int> DeleteAllAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Raised when a write would leave two entries sharing a facility, component, measure and
/// period. Allowing that would let the store answer a plan query with contradictory rows.
/// </summary>
public class DuplicateReportingPlanEntryException : Exception
{
    public DuplicateReportingPlanEntryException(
        string facilityId, string component, string measure, int? reportingMonth, int reportingYear)
        : base($"An entry already exists for facility '{facilityId}', component '{component}', "
               + $"measure '{measure}', {(reportingMonth is null ? $"{reportingYear}" : $"{reportingMonth}/{reportingYear}")}.")
    {
        FacilityId = facilityId;
        Component = component;
        Measure = measure;
        ReportingMonth = reportingMonth;
        ReportingYear = reportingYear;
    }

    public string FacilityId { get; }
    public string Component { get; }
    public string Measure { get; }
    public int? ReportingMonth { get; }
    public int ReportingYear { get; }
}

/// <summary>
/// Raised when an entry cannot be stored as a reporting plan entry.
/// </summary>
/// <remarks>
/// Rejecting here rather than leaving it to the database matters, because the failures are
/// silent ones: an entry stored against a period nothing queries is returned by no plan at
/// all, and the plan simply comes back short with nothing to indicate a row was skipped.
/// </remarks>
public class InvalidReportingPlanEntryException : Exception
{
    public InvalidReportingPlanEntryException(string message) : base(message)
    {
    }
}
