using System.Linq.Expressions;
using LantanaGroup.Link.MockDmrpApi.Application.Models;
using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.MockDmrpApi.Application.Services;

public class ReportingPlanService : IReportingPlanService
{
    private readonly IBaseEntityRepository<ReportingPlanEntryEntity> _repository;

    public ReportingPlanService(IBaseEntityRepository<ReportingPlanEntryEntity> repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ReportingPlanEntryEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await _repository.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<ReportingPlanEntryEntity> Records, PaginationMetadata Metadata)> GetByFacilityAsync(
        string facilityId, int pageSize, int pageNumber, CancellationToken cancellationToken)
    {
        facilityId = Trim(facilityId);

        var (records, metadata) = await _repository.SearchAsync(
            e => e.FacilityId == facilityId,
            nameof(ReportingPlanEntryEntity.CreateDate),
            Shared.Application.Enums.SortOrder.Descending,
            ClampPageSize(pageSize),
            ClampPageNumber(pageNumber),
            cancellationToken);

        return (records, metadata);
    }

    public async Task<(IReadOnlyList<ReportingPlanEntryEntity> Records, PaginationMetadata Metadata)> SearchAsync(
        ReportingPlanSearchCriteria criteria, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var (records, metadata) = await _repository.SearchAsync(
            BuildPredicate(criteria),
            ResolveSortBy(criteria.SortBy),
            criteria.SortOrder,
            ClampPageSize(criteria.PageSize),
            ClampPageNumber(criteria.PageNumber),
            cancellationToken);

        return (records, metadata);
    }

    public async Task<IReadOnlyList<ReportingPlanEntryEntity>> GetReportingPlanAsync(
        string component,
        string nhsnOrgId,
        string? measure,
        int? reportingMonth,
        int? reportingYear,
        CancellationToken cancellationToken)
    {
        // Component and facility always apply; the rest narrow only when supplied, so a
        // caller passing neither month nor year gets the whole plan for that component.
        //
        // Only entries actively being reported take part in a plan. An entry explicitly
        // marked as not reporting is equivalent to no entry at all, since the response
        // conveys enrollment by presence.
        nhsnOrgId = Trim(nhsnOrgId);
        component = Trim(component);
        measure = string.IsNullOrWhiteSpace(measure) ? null : Trim(measure);

        return await _repository.FindAsync(
            e => e.FacilityId == nhsnOrgId
                 && e.Component == component
                 && e.IsReporting == "Y"
                 && (measure == null || e.Measure.ToLower() == measure.ToLower())
                 && (reportingMonth == null || e.ReportingMonth == reportingMonth)
                 && (reportingYear == null || e.ReportingYear == reportingYear),
            cancellationToken);
    }

    public async Task<ReportingPlanEntryEntity> CreateAsync(ReportingPlanEntryEntity entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Normalize(entry);
        GuardRequiredFields(entry);
        GuardComponentAndPeriod(entry);
        await GuardNaturalKeyAsync(entry, excludeId: null, cancellationToken);

        entry.Id = Guid.NewGuid().ToString();

        try
        {
            return await _repository.AddAsync(entry, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The pre-check above narrows the window but does not close it; the unique
            // index is the actual guarantee. Translate so callers see one failure mode.
            throw new DuplicateReportingPlanEntryException(
                entry.FacilityId, entry.Component, entry.Measure, entry.ReportingMonth, entry.ReportingYear);
        }
    }

    public async Task<ReportingPlanEntryEntity?> UpdateAsync(ReportingPlanEntryEntity entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var existing = await _repository.FirstOrDefaultAsync(e => e.Id == entry.Id, cancellationToken);
        if (existing is null)
        {
            // Deliberately does not create. Update is update-only.
            return null;
        }

        Normalize(entry);
        GuardRequiredFields(entry);
        GuardComponentAndPeriod(entry);
        await GuardNaturalKeyAsync(entry, excludeId: entry.Id, cancellationToken);

        existing.FacilityId = entry.FacilityId;
        existing.Component = entry.Component;
        existing.Measure = entry.Measure;
        existing.ReportingMonth = entry.ReportingMonth;
        existing.ReportingYear = entry.ReportingYear;
        existing.IsReporting = entry.IsReporting;

        try
        {
            return await _repository.UpdateAsync(existing, cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new DuplicateReportingPlanEntryException(
                entry.FacilityId, entry.Component, entry.Measure, entry.ReportingMonth, entry.ReportingYear);
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await _repository.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        await _repository.DeleteAsync(existing, cancellationToken);
        return true;
    }

    public async Task<int> DeleteByFacilityAsync(string facilityId, CancellationToken cancellationToken)
    {
        facilityId = Trim(facilityId);

        var existing = await _repository.FindAsync(e => e.FacilityId == facilityId, cancellationToken);
        return await DeleteRangeAsync(existing, cancellationToken);
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAllAsync(cancellationToken);
        return await DeleteRangeAsync(existing, cancellationToken);
    }

    private async Task<int> DeleteRangeAsync(IReadOnlyList<ReportingPlanEntryEntity> entries, CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _repository.DeleteAsync(entry, cancellationToken);
        }

        return entries.Count;
    }

    /// <summary>
    /// Rejects an entry whose reporting period cannot be reported against.
    /// </summary>
    /// <remarks>
    /// Both components are reported monthly, so every entry carries a month. An entry stored
    /// with the wrong month is returned for no month at all, which is a silent failure: the
    /// row is there, the unique index is satisfied, and the plan simply comes back short.
    /// </remarks>
    /// <summary>
    /// Trims a value that takes part in the natural key.
    /// </summary>
    /// <remarks>
    /// The sanitizer keeps the space character, so without this <c>" HOB"</c> and <c>"HOB"</c>
    /// are two distinct measures. Both would store happily, and a plan seeded with the padded
    /// one would silently omit the measure a consumer is looking for -- no error anywhere,
    /// just a short plan. Applied to lookups as well as writes, so a padded query still finds
    /// a trimmed row.
    /// </remarks>
    private static string Trim(string? value) => value?.Trim() ?? string.Empty;

    /// <summary>Trims a filter, keeping "not supplied" distinct from "supplied as blank".</summary>
    private static string? TrimFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Brings an entry's key fields to the form they are stored and compared in.
    /// </summary>
    /// <remarks>
    /// Runs before both the cadence guard and the duplicate pre-check, so an entry is
    /// validated and compared as it will be persisted rather than as it arrived.
    /// </remarks>
    private static void Normalize(ReportingPlanEntryEntity entry)
    {
        entry.FacilityId = Trim(entry.FacilityId);
        entry.Component = Trim(entry.Component);
        entry.Measure = Trim(entry.Measure);
        entry.IsReporting = Trim(entry.IsReporting);
    }

    /// <summary>
    /// Rejects a key field that is empty once trimmed.
    /// </summary>
    /// <remarks>
    /// Trimming introduces this: a measure of <c>"   "</c> used to be stored verbatim and was
    /// merely useless, but it now trims to <c>""</c>, and an entry with no measure at all
    /// would satisfy every other rule here. The request annotations catch it over HTTP; this
    /// closes the same hole for a caller reaching the service directly.
    /// </remarks>
    private static void GuardRequiredFields(ReportingPlanEntryEntity entry)
    {
        if (string.IsNullOrEmpty(entry.FacilityId))
        {
            throw new InvalidReportingPlanEntryException("facilityId is required.");
        }

        if (string.IsNullOrEmpty(entry.Measure))
        {
            throw new InvalidReportingPlanEntryException("measure is required.");
        }
    }

    private static void GuardComponentAndPeriod(ReportingPlanEntryEntity entry)
    {
        if (!ReportingComponents.IsKnown(entry.Component))
        {
            throw new InvalidReportingPlanEntryException(
                $"Component '{entry.Component}' is not recognised. Expected one of: "
                + string.Join(", ", ReportingComponents.All) + ".");
        }

        // Accepted in any casing but stored canonically. The endpoints match the component
        // exactly, and a case-sensitive collation would otherwise make a row seeded as
        // "msc" invisible to /msc -- a difference no local run against SQL Server's
        // default case-insensitive collation would ever reveal.
        entry.Component = ReportingComponents.Normalize(entry.Component);

        if (entry.ReportingMonth is < 1 or > 12)
        {
            throw new InvalidReportingPlanEntryException(
                "reportingMonth must be between 1 and 12.");
        }
    }

    private async Task GuardNaturalKeyAsync(ReportingPlanEntryEntity entry, string? excludeId, CancellationToken cancellationToken)
    {
        var clash = await _repository.AnyAsync(
            e => e.FacilityId == entry.FacilityId
                 && e.Component == entry.Component
                 && e.Measure == entry.Measure
                 && e.ReportingMonth == entry.ReportingMonth
                 && e.ReportingYear == entry.ReportingYear
                 && (excludeId == null || e.Id != excludeId),
            cancellationToken);

        if (clash)
        {
            throw new DuplicateReportingPlanEntryException(
                entry.FacilityId, entry.Component, entry.Measure, entry.ReportingMonth, entry.ReportingYear);
        }
    }

    private static Expression<Func<ReportingPlanEntryEntity, bool>> BuildPredicate(ReportingPlanSearchCriteria criteria)
    {
        // Trimmed for the same reason writes are: a padded filter must still match the row a
        // padded create would now have stored trimmed.
        criteria.FacilityId = TrimFilter(criteria.FacilityId);
        criteria.Component = TrimFilter(criteria.Component);
        criteria.Measure = TrimFilter(criteria.Measure);
        criteria.IsReporting = TrimFilter(criteria.IsReporting);

        return e =>
            (criteria.FacilityId == null || e.FacilityId == criteria.FacilityId)
            && (criteria.Component == null || e.Component == criteria.Component)
            && (criteria.Measure == null || e.Measure.ToLower() == criteria.Measure.ToLower())
            && (criteria.ReportingMonth == null || e.ReportingMonth == criteria.ReportingMonth)
            && (criteria.ReportingYear == null || e.ReportingYear == criteria.ReportingYear)
            && (criteria.IsReporting == null || e.IsReporting == criteria.IsReporting);
    }

    /// <summary>
    /// Maps the closed sort enum onto a property name. Anything outside the enum is a
    /// programming error rather than client input, but it is rejected here too: the
    /// shared repository would otherwise turn an unknown name into a 500.
    /// </summary>
    private static string ResolveSortBy(ReportingPlanSortBy sortBy) => sortBy switch
    {
        ReportingPlanSortBy.FacilityId => nameof(ReportingPlanEntryEntity.FacilityId),
        ReportingPlanSortBy.Component => nameof(ReportingPlanEntryEntity.Component),
        ReportingPlanSortBy.Measure => nameof(ReportingPlanEntryEntity.Measure),
        ReportingPlanSortBy.ReportingMonth => nameof(ReportingPlanEntryEntity.ReportingMonth),
        ReportingPlanSortBy.ReportingYear => nameof(ReportingPlanEntryEntity.ReportingYear),
        ReportingPlanSortBy.CreateDate => nameof(ReportingPlanEntryEntity.CreateDate),
        ReportingPlanSortBy.ModifyDate => nameof(ReportingPlanEntryEntity.ModifyDate),
        _ => throw new ArgumentOutOfRangeException(nameof(sortBy), sortBy, "Unsupported sort field.")
    };

    /// <summary>
    /// Brings paging into range for a direct caller.
    /// </summary>
    /// <remarks>
    /// No longer reachable over HTTP: the controller carries <c>[Range]</c> annotations that
    /// reject an out-of-range page with a 400 before the action runs, which is what QA's cases
    /// assert. Kept as a floor for callers that reach the service directly, so a bad value
    /// cannot turn into a negative <c>Skip</c> in the repository.
    /// </remarks>
    private static int ClampPageSize(int pageSize) => pageSize switch
    {
        < 1 => ReportingPlanSearchCriteria.DefaultPageSize,
        > ReportingPlanSearchCriteria.MaxPageSize => ReportingPlanSearchCriteria.MaxPageSize,
        _ => pageSize
    };

    private static int ClampPageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;
}
