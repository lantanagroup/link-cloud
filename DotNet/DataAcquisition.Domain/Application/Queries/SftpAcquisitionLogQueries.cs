using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.SerDes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;

public interface ISftpAcquisitionLogQueries
{
    Task<SftpAcquisitionLog?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<SftpAcquisitionLog?> GetByExternalIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PagedSftpAcquisitionLogModel> SearchAsync(SftpLogSearchParameters queryParameters,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets pending logs ready for processing (ScheduledDate is null or in the past).
    /// </summary>
    Task<List<SftpAcquisitionLog>> GetPendingLogsAsync(
        SftpAcquisitionType acquisitionType,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets failed logs eligible for retry (under max retry attempts and ready to process).
    /// </summary>
    Task<List<SftpAcquisitionLog>> GetFailedLogsForRetryAsync(
        SftpAcquisitionType acquisitionType,
        int maxRetries,
        int limit,
        CancellationToken cancellationToken);
}

public class SftpAcquisitionLogQueries(
    ILogger<SftpAcquisitionLogQueries> logger,
    DataAcquisitionDbContext dbContext) : ISftpAcquisitionLogQueries
{
    public async Task<SftpAcquisitionLog?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        try
        {
            var sftpLog = await dbContext.SftpAcquisitionLogs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken: cancellationToken);
            return sftpLog;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error retrieving SFTP Acquisition Log with Id: {Id}", id);
            throw;
        }

    }

    public async Task<SftpAcquisitionLog?> GetByExternalIdAsync(Guid id, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag(DiagnosticNames.EntityId, id);

        try
        {
            var sftpLog = await dbContext.SftpAcquisitionLogs.FirstOrDefaultAsync(x => x.ExternalId == id, cancellationToken: cancellationToken);
            return sftpLog;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error retrieving SFTP Acquisition Log with Id: {Id}", id);
            throw;
        }

    }

    public async Task<PagedSftpAcquisitionLogModel> SearchAsync(SftpLogSearchParameters queryParameters, CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.AddTag(DiagnosticNames.SearchParameters,
            JsonSerializer.Serialize(queryParameters, LinkFhirSerializerOptions.ActivityTagging));

        try
        {
            var query = dbContext.SftpAcquisitionLogs
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(queryParameters.FacilityId))
                query = query.Where(x => x.FacilityId == queryParameters.FacilityId);

            if (queryParameters.Status.HasValue)
                query = query.Where(x => x.Status == queryParameters.Status.Value);

            if (queryParameters.AcquisitionType.HasValue)
                query = query.Where(x => x.AcquisitionType == queryParameters.AcquisitionType.Value);

            if (queryParameters.SubType.HasValue)
                query = query.Where(x => x.SubType == queryParameters.SubType.Value);

            query = queryParameters.SortOrder switch
            {
                SortOrder.Ascending => query.OrderBy(SetSortBy<SftpAcquisitionLog>(queryParameters.SortBy)),
                SortOrder.Descending => query.OrderByDescending(SetSortBy<SftpAcquisitionLog>(queryParameters.SortBy)),
                _ => query
            };

            var total = await query.CountAsync(cancellationToken);

            var logs = await query
                .Skip((queryParameters.PageNumber - 1) * queryParameters.PageSize)
                .Take(queryParameters.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedSftpAcquisitionLogModel
            {
                Metadata = new PaginationMetadata
                {
                    PageNumber = queryParameters.PageNumber,
                    PageSize = queryParameters.PageSize,
                    TotalCount = total,
                    TotalPages = (long)Math.Ceiling(total / (double)queryParameters.PageSize)
                },
                Records = logs.Select(x => x.ToModel()).ToList()
            };
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Error retrieving SFTP Acquisition Logs");
            throw;
        }
    }

    public async Task<List<SftpAcquisitionLog>> GetPendingLogsAsync(
        SftpAcquisitionType acquisitionType,
        int limit,
        CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag("acquisitionType", acquisitionType.ToString());

        var now = DateTime.UtcNow;
        return await dbContext.SftpAcquisitionLogs
            .Where(x => x.AcquisitionType == acquisitionType
                        && x.Status == RequestStatus.Pending
                        && (x.ScheduledDate == null || x.ScheduledDate <= now))
            .OrderBy(x => x.ScheduledDate ?? DateTime.MinValue)
            .ThenBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SftpAcquisitionLog>> GetFailedLogsForRetryAsync(
        SftpAcquisitionType acquisitionType,
        int maxRetries,
        int limit,
        CancellationToken cancellationToken)
    {
        using var activity = Activity.Current?.Source.StartActivity();
        activity?.SetTag("acquisitionType", acquisitionType.ToString());

        var now = DateTime.UtcNow;
        return await dbContext.SftpAcquisitionLogs
            .Where(x => x.AcquisitionType == acquisitionType
                        && x.Status == RequestStatus.Failed
                        && (x.RetryAttempts ?? 0) < maxRetries
                        && (x.ScheduledDate == null || x.ScheduledDate <= now))
            .OrderBy(x => x.ScheduledDate ?? DateTime.MinValue)
            .ThenBy(x => x.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private Expression<Func<T, object>> SetSortBy<T>(string? sortBy)
    {
        var type = typeof(T);
        var inputSortBy = sortBy?.Trim();
        var sortKey = "ProcessDate"; // default

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
}