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
    /// The entries making up a facility's monthly plan for one component -- MSC today.
    /// </summary>
    /// <remarks>
    /// Only entries actively being reported are returned; the absence of a measure is what
    /// conveys "not enrolled".
    /// </remarks>
    Task<IReadOnlyList<ReportingPlanEntryEntity>> GetMonthlyReportingPlanAsync(
        string component, string facilityId, int reportingMonth, int reportingYear,
        CancellationToken cancellationToken);

    /// <summary>
    /// The entries making up a facility's annual plan for one component -- PS today.
    /// </summary>
    /// <remarks>
    /// Annual components carry no reporting month, so this matches on the year alone. Same
    /// absence semantics as the monthly plan.
    /// </remarks>
    Task<IReadOnlyList<ReportingPlanEntryEntity>> GetAnnualReportingPlanAsync(
        string component, string facilityId, int reportingYear, CancellationToken cancellationToken);

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
/// Raised when an entry's component and reporting period disagree.
/// </summary>
/// <remarks>
/// The rule is conditional -- a monthly component must carry a month, an annual one must
/// not -- so it cannot be expressed as a column constraint or a range annotation. Rejecting
/// it here matters: a patient-safety entry saved with a stray month still satisfies the
/// unique index, but would sit in a different index slot than the annual query looks in and
/// so become invisible rather than wrong.
/// </remarks>
public class InvalidReportingPlanEntryException : Exception
{
    public InvalidReportingPlanEntryException(string message) : base(message)
    {
    }
}
