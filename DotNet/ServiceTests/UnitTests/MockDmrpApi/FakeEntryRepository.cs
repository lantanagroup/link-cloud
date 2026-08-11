using LantanaGroup.Link.MockDmrpApi.Domain.Entities;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// ServiceTests globally imports Hl7.Fhir.Model, which has its own Task and Expression
// types. The shared repository interface applies the same Task alias for the same reason.
using Task = System.Threading.Tasks.Task;
using Expression = System.Linq.Expressions.Expression;

namespace UnitTests.MockDmrpApi;

/// <summary>
/// In-memory stand-in for <see cref="IBaseEntityRepository{T}"/>.
/// </summary>
/// <remarks>
/// A hand-written fake rather than a mock because the behaviour under test is largely the
/// predicates and sort expressions the service builds. A mock would record that
/// <c>SearchAsync</c> was called with "some expression" and assert nothing about whether
/// that expression selects the right rows; this executes them.
/// <para>
/// It intentionally does not enforce the unique index -- that is the database's job and is
/// covered by integration tests. The service's own pre-check is what these tests exercise.
/// </para>
/// </remarks>
public class FakeEntryRepository : IBaseEntityRepository<ReportingPlanEntryEntity>
{
    private readonly List<ReportingPlanEntryEntity> _entries = [];

    public IReadOnlyList<ReportingPlanEntryEntity> Entries => _entries;

    /// <summary>Sort field name most recently passed to <see cref="SearchAsync"/>.</summary>
    public string? LastSortBy { get; private set; }

    public SortOrder? LastSortOrder { get; private set; }

    public int LastPageSize { get; private set; }

    public int LastPageNumber { get; private set; }

    public void Seed(params ReportingPlanEntryEntity[] entries)
    {
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.Id))
            {
                entry.Id = Guid.NewGuid().ToString();
            }

            if (entry.CreateDate == default)
            {
                entry.CreateDate = DateTime.UtcNow;
            }

            _entries.Add(entry);
        }
    }

    public Task<ReportingPlanEntryEntity> AddAsync(ReportingPlanEntryEntity entity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(entity.Id))
        {
            entity.Id = Guid.NewGuid().ToString();
        }

        entity.CreateDate = DateTime.UtcNow;
        _entries.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<ReportingPlanEntryEntity> UpdateAsync(ReportingPlanEntryEntity entity, CancellationToken cancellationToken = default)
    {
        var index = _entries.FindIndex(e => e.Id == entity.Id);
        if (index >= 0)
        {
            entity.ModifyDate = DateTime.UtcNow;
            _entries[index] = entity;
        }

        return Task.FromResult(entity);
    }

    public Task DeleteAsync(ReportingPlanEntryEntity? entity, CancellationToken cancellationToken)
    {
        if (entity is not null)
        {
            _entries.RemoveAll(e => e.Id == entity.Id);
        }

        return Task.CompletedTask;
    }

    public Task<List<ReportingPlanEntryEntity>> FindAsync(
        System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_entries.AsQueryable().Where(predicate).ToList());
    }

    public Task<ReportingPlanEntryEntity?> FirstOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_entries.AsQueryable().FirstOrDefault(predicate));
    }

    public Task<bool> AnyAsync(
        System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_entries.AsQueryable().Any(predicate));
    }

    public Task<List<ReportingPlanEntryEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_entries.ToList());
    }

    public Task<(List<ReportingPlanEntryEntity>, PaginationMetadata)> SearchAsync(
        System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate,
        string? sortBy,
        SortOrder? sortOrder,
        int pageSize,
        int pageNumber,
        CancellationToken cancellationToken = default)
    {
        LastSortBy = sortBy;
        LastSortOrder = sortOrder;
        LastPageSize = pageSize;
        LastPageNumber = pageNumber;

        IQueryable<ReportingPlanEntryEntity> query = _entries.AsQueryable().Where(predicate);
        var count = query.Count();

        if (sortBy is not null)
        {
            // Mirrors the shared repository, which builds the sort expression by property
            // name and throws for a name that is not a property.
            var parameter = Expression.Parameter(typeof(ReportingPlanEntryEntity), "e");
            var property = Expression.Property(parameter, sortBy);
            var selector = Expression.Lambda<Func<ReportingPlanEntryEntity, object>>(
                Expression.Convert(property, typeof(object)), parameter);

            query = sortOrder == SortOrder.Ascending
                ? Queryable.OrderBy(query, selector)
                : Queryable.OrderByDescending(query, selector);
        }

        var results = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult((results, new PaginationMetadata(pageSize, pageNumber, count)));
    }

    // ---- Not exercised by these tests. ----

    public ReportingPlanEntryEntity Add(ReportingPlanEntryEntity entity) => throw new NotSupportedException();
    public Task RemoveAsync(ReportingPlanEntryEntity entity) => throw new NotSupportedException();
    public ReportingPlanEntryEntity Get(object id) => throw new NotSupportedException();
    public Task<ReportingPlanEntryEntity> GetAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportingPlanEntryEntity> FirstAsync(System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportingPlanEntryEntity?> SingleOrDefaultAsync(System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ReportingPlanEntryEntity> SingleAsync(System.Linq.Expressions.Expression<Func<ReportingPlanEntryEntity, bool>> predicate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ReportingPlanEntryEntity Update(ReportingPlanEntryEntity entity) => throw new NotSupportedException();
    public void Delete(object id) => throw new NotSupportedException();
    public Task DeleteAsync(object id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<HealthCheckResult> HealthCheck(int eventId) => throw new NotSupportedException();
    public void StartTransaction() => throw new NotSupportedException();
    public void CommitTransaction() => throw new NotSupportedException();
    public void RollbackTransaction() => throw new NotSupportedException();
    public Task StartTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
