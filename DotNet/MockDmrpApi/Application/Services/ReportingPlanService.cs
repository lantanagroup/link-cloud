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

    public async Task<IReadOnlyList<ReportingPlanEntryEntity>> GetMonthlyReportingPlanAsync(
        string component, string facilityId, int reportingMonth, int reportingYear,
        CancellationToken cancellationToken)
    {
        // Only entries actively being reported take part in a plan. An entry explicitly
        // marked as not reporting is equivalent to no entry at all, since the response
        // conveys enrollment by presence.
        return await _repository.FindAsync(
            e => e.FacilityId == facilityId
                 && e.Component == component
                 && e.ReportingMonth == reportingMonth
                 && e.ReportingYear == reportingYear
                 && e.IsReporting == "Y",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ReportingPlanEntryEntity>> GetAnnualReportingPlanAsync(
        string component, string facilityId, int reportingYear, CancellationToken cancellationToken)
    {
        // No month in the predicate: an annual component's entries carry none, and matching
        // on one would exclude every row it is supposed to return.
        return await _repository.FindAsync(
            e => e.FacilityId == facilityId
                 && e.Component == component
                 && e.ReportingYear == reportingYear
                 && e.IsReporting == "Y",
            cancellationToken);
    }

    public async Task<ReportingPlanEntryEntity> CreateAsync(ReportingPlanEntryEntity entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

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
    /// Rejects an entry whose reporting period does not match its component's cadence.
    /// </summary>
    /// <remarks>
    /// This cannot be a column constraint or a range annotation, because whether a month is
    /// required depends on the component. It has to be enforced, not merely documented: a
    /// patient-safety entry saved with a stray month satisfies the unique index perfectly
    /// well, but the annual query does not filter on month, so the row would be returned for
    /// every month -- or, with the month wrong on a monthly entry, returned for none. Both
    /// failures are silent.
    /// </remarks>
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

        var monthRequired = ReportingComponents.RequiresReportingMonth(entry.Component);

        if (monthRequired && entry.ReportingMonth is null)
        {
            throw new InvalidReportingPlanEntryException(
                $"Component '{entry.Component}' is reported monthly, so reportingMonth is required.");
        }

        if (!monthRequired && entry.ReportingMonth is not null)
        {
            throw new InvalidReportingPlanEntryException(
                $"Component '{entry.Component}' is reported annually, so reportingMonth must be omitted.");
        }

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

    private static int ClampPageSize(int pageSize) => pageSize switch
    {
        < 1 => ReportingPlanSearchCriteria.DefaultPageSize,
        > ReportingPlanSearchCriteria.MaxPageSize => ReportingPlanSearchCriteria.MaxPageSize,
        _ => pageSize
    };

    private static int ClampPageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;
}
