using LantanaGroup.Link.Census.Application.Models;
using LantanaGroup.Link.Census.Application.Models.Enums;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Context;
using LantanaGroup.Link.Shared.Application.Enums; // Important for SQL functions
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;
using System.Reflection;
using Expression = System.Linq.Expressions.Expression;
using PatientEvent = LantanaGroup.Link.Census.Domain.Entities.POI.PatientEvent;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Census.Domain.Queries;

public interface IPatientEventQueries
{
    Task<PatientEvent> GetLatestEventByFacilityAndPatientId(
        string facilityId,
        string patientId,
        CancellationToken cancellationToken);

    Task<IEnumerable<PatientEvent>> GetPatientEvents(
        string facilityId,
        string? correlationId = default,
        DateTime? startDate = default,
        DateTime? endDate = default,
        CancellationToken cancellationToken = default);

    Task<PagedConfigModel<PatientEventModel>> GetPagedPatientEvents(
        string facilityId,
        string? correlationId = default,
        DateTime? startDate = default,
        DateTime? endDate = default,
        string? sortBy = default,
        SortOrder? sortOrder = default,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default);

    Task DeletePatientEventByCorrelationId(
        string correlationId,
        CancellationToken cancellationToken);

    Task<IEnumerable<PatientEventModel>> GetAdmittedPatientEventModelsByDateRange(
        string facilityId,
        DateTime startDateTime,
        DateTime endDateTime,
        CancellationToken cancellationToken = default);

    Task<IDbContextTransaction> StartTransaction(CancellationToken cancellationToken = default);

    Task CommitTransaction(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken = default);

    Task RollbackTransaction(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken = default);
}

public class PatientEventQueries : IPatientEventQueries
{
    private readonly CensusContext _context;

    public PatientEventQueries(CensusContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<PatientEventModel>> GetAdmittedPatientEventModelsByDateRange(string facilityId, DateTime startDateTime, DateTime endDateTime, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));

        if (startDateTime == default)
            throw new ArgumentException("Start date cannot be default.", nameof(startDateTime));

        if (endDateTime == default)
            throw new ArgumentException("End date cannot be default.", nameof(endDateTime));

        var admitEventTypes = new List<EventType>
        {
            EventType.FHIRListAdmit,
            EventType.A01
        };

        var dischargeEventTypes = new List<EventType>
        {
            EventType.FHIRListDischarge,
            EventType.A03
        };

        // Get all admit and discharge events within the date range for the facility
        var eventsWithinRange = await GetPatientEvents(facilityId, null, startDateTime, endDateTime, cancellationToken);

        // Group events by patient ID
        var patientEvents = eventsWithinRange
            .GroupBy(x => x.SourcePatientId)
            .ToDictionary(g => g.Key, g => g.ToList());


        // Find the patients who have an admit event within the date range
        // and either have no discharge event or the latest event is an admit event
        var result = new List<PatientEventModel>();

        foreach (var patientGroup in patientEvents)
        {
            var sourcePatientId = patientGroup.Key;
            var events = patientGroup.Value;

            // Check if there's at least one admit event in the range
            var hasAdmitEvent = events.Any(e => admitEventTypes.Contains(e.EventType));

            if (hasAdmitEvent)
            {
                // Find the latest event for this patient
                var latestEvent = events
                    .OrderByDescending(e => e.EventDate)
                    .FirstOrDefault();

                // If the latest event is an admit event, include this patient
                if (latestEvent != null && admitEventTypes.Contains(latestEvent.EventType))
                {
                    result.Add(PatientEventModel.FromDomain(latestEvent));
                }
            }
        }

        return result;
    }

    public async Task DeletePatientEventByCorrelationId(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
        }

        bool isInMemory = _context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;
        if (isInMemory)
        {
            await DeletePatientEventByCorrelationIdInMemory(correlationId, cancellationToken);
        }
        else
        {
            await DeletePatientEventByCorrelationIdBatch(correlationId, cancellationToken);
        }
    }

    private async Task DeletePatientEventByCorrelationIdInMemory(string correlationId, CancellationToken cancellationToken)
    {
        var entities = await _context.PatientEvents
            .Where(x => x.CorrelationId == correlationId)
            .ToListAsync(cancellationToken);
        _context.PatientEvents.RemoveRange(entities);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task DeletePatientEventByCorrelationIdBatch(string correlationId, CancellationToken cancellationToken)
    {
        await _context.PatientEvents
            .Where(x => x.CorrelationId == correlationId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<PatientEvent> GetLatestEventByFacilityAndPatientId(string facilityId, string patientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("Patient ID cannot be null or empty.", nameof(patientId));
        }

        return await _context.PatientEvents
            .Where(x => x.FacilityId == facilityId && x.SourcePatientId == patientId)
            .OrderByDescending(x => x.EventDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PagedConfigModel<PatientEventModel>> GetPagedPatientEvents(
        string facilityId,
        string? correlationId = default,
        DateTime? startDate = default,
        DateTime? endDate = default,
        string? sortBy = default,
        SortOrder? sortOrder = default,
        int pageSize = 10,
        int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var query = GetPatientQuery(facilityId, correlationId, startDate, endDate);

        query = sortOrder switch
        {
            SortOrder.Ascending => query.OrderBy(SetSortBy<PatientEvent>(sortBy)),
            SortOrder.Descending => query.OrderByDescending(SetSortBy<PatientEvent>(sortBy)),
            _ => query
        };

        // Apply pagination
        var total = await query.CountAsync();
        var pagedRecords = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(PatientEventModel.FromDomain)
            .ToList();

        return new PagedConfigModel<PatientEventModel>
        {
            Metadata = new PaginationMetadata
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
                TotalPages = countPages(total, pageSize),
            },
            Records = pagedRecords
        };
    }

    public async Task<IEnumerable<PatientEvent>> GetPatientEvents(
        string facilityId,
        string? correlationId = default,
        DateTime? startDate = default,
        DateTime? endDate = default,
        CancellationToken cancellationToken = default)
    {
        var combinedQuery = GetPatientQuery(facilityId, correlationId, startDate, endDate);

        return await combinedQuery.ToListAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> StartTransaction(CancellationToken cancellationToken = default)
    {
        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransaction(IDbContextTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackTransaction(IDbContextTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await transaction.RollbackAsync(cancellationToken);
    }

    #region Private Methods
    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var type = typeof(T);
        var inputSortBy = sortBy?.Trim();
        string sortKey = "Id"; // default

        if (!string.IsNullOrEmpty(inputSortBy))
        {
            var prop = type.GetProperty(inputSortBy, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                sortKey = prop.Name;
            }
        }

        var parameter = Expression.Parameter(type, "p");
        var property = Expression.Property(parameter, sortKey);
        var converted = Expression.Convert(property, typeof(object));
        return Expression.Lambda<Func<T, object>>(converted, parameter);
    }

    private IQueryable<PatientEvent> GetPatientQuery(string facilityId, string? correlationId, DateTime? startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
        {
            throw new ArgumentException("Facility ID cannot be null or empty.", nameof(facilityId));
        }

        var query = _context.PatientEvents.AsQueryable();
        query = query.Where(x => x.FacilityId == facilityId);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(x => x.CorrelationId == correlationId);
        }

        // For regular events or as a fallback, filter by CreateDate
        var baseQuery = query;

        if (startDate.HasValue && startDate != default)
        {
            baseQuery = baseQuery.Where(x => x.EventDate >= startDate.Value);
        }

        if (endDate.HasValue && endDate != default)
        {
            baseQuery = baseQuery.Where(x => x.EventDate <= endDate.Value);
        }

        // Create specific queries for FHIR events with date filtering
        var queries = new List<IQueryable<PatientEvent>>();

        // Add the base query for non-FHIR events
        queries.Add(baseQuery.Where(x =>
            x.EventType != EventType.FHIRListAdmit &&
            x.EventType != EventType.FHIRListDischarge));

        // Add query for FHIRListAdmit events
        var admitQuery = query.Where(x => x.EventType == EventType.FHIRListAdmit);

        if (startDate.HasValue && startDate != default)
        {
            admitQuery = admitQuery.Where(x => x.EventDate >= startDate.Value);
        }

        if (endDate.HasValue && endDate != default)
        {
            admitQuery = admitQuery.Where(x => x.EventDate <= endDate.Value);
        }

        queries.Add(admitQuery);

        // Add query for FHIRListDischarge events
        var dischargeQuery = query.Where(x => x.EventType == EventType.FHIRListDischarge);

        if (startDate.HasValue && startDate != default)
        {
            dischargeQuery = dischargeQuery.Where(x => x.EventDate >= startDate.Value);
        }

        if (endDate.HasValue && endDate != default)
        {
            dischargeQuery = dischargeQuery.Where(x => x.EventDate <= endDate.Value);
        }

        queries.Add(dischargeQuery);

        // Combine all the queries using Union
        var combinedQuery = queries[0];
        for (int i = 1; i < queries.Count; i++)
        {
            combinedQuery = combinedQuery.Union(queries[i]);
        }

        return combinedQuery;
    }

    private int countPages(int totalRecords, int recordsPerPage)
    {
        return (totalRecords / recordsPerPage) + (totalRecords % recordsPerPage > 0 ? 1 : 0);
    }
    #endregion
}