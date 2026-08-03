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
    /// The entries that make up a facility's plan for one period. Only entries actively
    /// being reported are returned; the absence of a measure is what conveys "not enrolled".
    /// </summary>
    Task<IReadOnlyList<ReportingPlanEntryEntity>> GetReportingPlanAsync(
        string facilityId, int reportingMonth, int reportingYear, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an entry.
    /// </summary>
    /// <exception cref="DuplicateReportingPlanEntryException">
    /// An entry already exists for the same facility, measure and period.
    /// </exception>
    Task<ReportingPlanEntryEntity> CreateAsync(ReportingPlanEntryEntity entry, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing entry. Returns <c>null</c> when no entry has the supplied
    /// identifier -- this never creates one.
    /// </summary>
    /// <exception cref="DuplicateReportingPlanEntryException">
    /// The update would collide with another entry's facility, measure and period.
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
/// Raised when a write would leave two entries sharing a facility, measure and period.
/// Allowing that would let the store answer a reporting plan query with contradictory rows.
/// </summary>
public class DuplicateReportingPlanEntryException : Exception
{
    public DuplicateReportingPlanEntryException(string facilityId, string measure, int reportingMonth, int reportingYear)
        : base($"An entry already exists for facility '{facilityId}', measure '{measure}', {reportingMonth}/{reportingYear}.")
    {
        FacilityId = facilityId;
        Measure = measure;
        ReportingMonth = reportingMonth;
        ReportingYear = reportingYear;
    }

    public string FacilityId { get; }
    public string Measure { get; }
    public int ReportingMonth { get; }
    public int ReportingYear { get; }
}
