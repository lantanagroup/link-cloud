using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Link.UI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// Acquisition logs, sFTP logs, audit events, and the Kafka/Grafana links. Reads and the
/// log actions go through LinkSDK, including facility-wide disable and restore.
/// </summary>
public sealed class AcquisitionCountLoad
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public long FailedTotal { get; init; }
    public IReadOnlyList<TrendDay> Days { get; init; } = [];

    public static AcquisitionCountLoad Failed(string error) => new() { Error = error };

    public static AcquisitionCountLoad Ready(long failedTotal, IReadOnlyList<TrendDay> days) =>
        new() { Ok = true, FailedTotal = failedTotal, Days = days };
}

public sealed class AuditErrorLoad
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public long Errors { get; init; }

    public static AuditErrorLoad Failed(string error) => new() { Error = error };

    public static AuditErrorLoad Ready(long errors) => new() { Ok = true, Errors = errors };
}

public sealed class LogsService
{
    public const string AcquisitionNotConfigured =
        "Data acquisition service URL is not configured (ServiceRegistry:DataAcquisitionServiceUrl).";
    public const string AuditNotConfigured =
        "Audit service URL is not configured (ServiceRegistry:AuditServiceUrl).";

    private readonly IDataAcquisitionServiceClient? _acquisition;
    private readonly IAuditServiceClient? _audit;
    private readonly LinkUiFeatureOptions _options;
    private readonly LogsLinkOptions _links;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LogsService> _logger;

    public LogsService(
        IDataAcquisitionServiceClient? acquisition,
        IAuditServiceClient? audit,
        IOptions<LinkUiFeatureOptions> options,
        IOptions<LogsLinkOptions> links,
        IMemoryCache cache,
        ILogger<LogsService> logger)
    {
        _acquisition = acquisition;
        _audit = audit;
        _options = options.Value;
        _links = links.Value;
        _cache = cache;
        _logger = logger;
    }

    public static LogsService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        return new LogsService(
            Blank(registry.DataAcquisitionServiceUrl) ? null : services.GetRequiredService<IDataAcquisitionServiceClient>(),
            Blank(registry.AuditServiceUrl) ? null : services.GetRequiredService<IAuditServiceClient>(),
            services.GetRequiredService<IOptions<LinkUiFeatureOptions>>(),
            services.GetRequiredService<IOptions<LogsLinkOptions>>(),
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<ILogger<LogsService>>());
    }

    public LogsHomePage LoadHome()
    {
        LogsRules.TryExternalUrl(_links.KafkaUiUrl, out var kafka);
        LogsRules.TryExternalUrl(_links.GrafanaUrl, out var grafana);
        return new LogsHomePage
        {
            AcquisitionConfigured = _acquisition is not null,
            AuditConfigured = _audit is not null,
            KafkaUrl = string.IsNullOrEmpty(kafka) ? null : kafka,
            GrafanaUrl = string.IsNullOrEmpty(grafana) ? null : grafana
        };
    }

    public KafkaPage LoadKafka()
    {
        LogsRules.TryExternalUrl(_links.KafkaUiUrl, out var kafka);
        return new KafkaPage { Url = string.IsNullOrEmpty(kafka) ? null : kafka };
    }

    public async Task<AcquisitionCountLoad> LoadAcquisitionCountsAsync(int days, CancellationToken cancellationToken)
    {
        if (_acquisition is null)
            return AcquisitionCountLoad.Failed(AcquisitionNotConfigured);

        try
        {
            var response = await _acquisition.GetActivityCountsAsync(
                new AcquisitionActivityCountRequest { Days = days },
                cancellationToken);
            if (!response.IsSuccessStatusCode || response.Body is null)
                return AcquisitionCountLoad.Failed(FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody));

            var daysInWindow = response.Body.Days.Select(day => new TrendDay
            {
                Day = day.Day,
                Reachable = true,
                Count = day.Total,
                Failed = day.Failed
            }).ToList();
            return AcquisitionCountLoad.Ready(response.Body.FailedTotal, daysInWindow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition activity counts failed");
            return AcquisitionCountLoad.Failed("Data acquisition service call failed.");
        }
    }

    public async Task<AuditErrorLoad> LoadAuditErrorsAsync(int hours, CancellationToken cancellationToken)
    {
        if (_audit is null)
            return AuditErrorLoad.Failed(AuditNotConfigured);

        try
        {
            var response = await _audit.GetErrorCountAsync(hours, cancellationToken);
            if (!response.IsSuccessStatusCode || response.Body is null)
                return AuditErrorLoad.Failed(FacilityFormRules.ServiceMessage("Audit", response.StatusCode, response.RawBody));
            return AuditErrorLoad.Ready(response.Body.Errors);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit error count failed");
            return AuditErrorLoad.Failed("Audit service call failed.");
        }
    }

    public async Task<AcquisitionLogListPage> LoadAcquisitionAsync(AcquisitionQuery? query, CancellationToken cancellationToken)
    {
        var search = LogsRules.Prepare(query, _options.NumericOnlyFacilityId, DateTime.UtcNow);
        var page = new AcquisitionLogListPage { Query = query ?? new AcquisitionQuery(), Search = search };
        if (search.Error is not null)
        {
            page.LoadError = search.Error;
            return page;
        }

        if (_acquisition is null)
        {
            page.LoadError = AcquisitionNotConfigured;
            return page;
        }

        try
        {
            var response = await _acquisition.SearchAcquisitionLogsAsync(ToQuery(search), cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent)
            {
                page.Paging = Bar(null, search.Page, search.PageSize, 0);
            }
            else if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
            }
            else
            {
                page.Paging = Bar(response.Body.Metadata, search.Page, search.PageSize, response.Body.Records.Count);
                page.Logs = response.Body.Records.Select(MapRow).ToList();
            }

            if (page.LoadError is null && search.ReportId is not null)
                page.CountsNote = await CountsAsync(page, search, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log search failed");
            page.LoadError = "Data acquisition service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<AcquisitionDetailPage> LoadAcquisitionDetailAsync(long id, int referencePage, CancellationToken cancellationToken)
    {
        var page = new AcquisitionDetailPage
        {
            Id = id,
            MinAgeHours = LogsRules.DefaultMinAgeHours
        };
        if (id <= 0)
        {
            page.NotFound = true;
            page.LoadError = "Log id is not a valid id.";
            return page;
        }

        if (_acquisition is null)
        {
            page.LoadError = AcquisitionNotConfigured;
            return page;
        }

        try
        {
            var response = await _acquisition.GetAcquisitionLogByIdAsync(id, cancellationToken);
            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                page.NotFound = true;
                page.LoadError = "That acquisition log was not found.";
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
                return page;
            }

            var log = response.Body;
            var status = log.Status?.ToString() ?? string.Empty;
            page.FacilityId = log.FacilityId ?? string.Empty;
            page.PatientId = log.PatientId ?? string.Empty;
            page.ReportId = log.ReportTrackingId;
            page.ReportLink = ReportLink(log.FacilityId, log.ReportTrackingId);
            page.Status = status;
            page.CanProcess = LogsRules.CanProcess(status);
            page.CanCancel = LogsRules.CanCancel(status, log.CreateDate, 0, DateTime.UtcNow);
            page.Phase = log.QueryPhase?.ToString() ?? string.Empty;
            page.Priority = log.Priority ?? string.Empty;
            page.CorrelationId = log.CorrelationId;
            page.TraceId = log.TraceId;
            page.FhirVersion = log.FhirVersion;
            page.RetryAttempts = log.RetryAttempts ?? 0;
            page.Execution = FacilityViewRules.When(log.ExecutionDate);
            page.Created = FacilityViewRules.When(log.CreateDate);
            page.Completed = FacilityViewRules.When(log.CompletionDate);
            page.CompletionMilliseconds = log.CompletionTimeMilliseconds;
            page.ReferenceLog = log.IsReferenceLog;
            page.ReferenceCount = log.ReferenceResourceCount;
            page.Resources = Join(log.ResourceTypes);
            var acquired = log.ResourceAcquiredIds ?? [];
            page.AcquiredIds = acquired.Take(LogsRules.MaxAcquiredIds).ToList();
            page.AcquiredHidden = Math.Max(0, acquired.Count - page.AcquiredIds.Count);
            page.Queries = (log.FhirQuery ?? []).Select(item => new FhirQueryRow
            {
                Type = item.QueryType.ToString(),
                Resources = Join(item.ResourceTypes),
                Parameters = Join(item.QueryParameters)
            }).ToList();

            var notes = await _acquisition.GetAcquisitionLogNotesAsync(id, cancellationToken);
            if (notes.IsSuccessStatusCode)
                page.Notes = notes.Body ?? log.Notes ?? [];
            else
            {
                page.Notes = log.Notes ?? [];
                page.NotesError = FacilityFormRules.ServiceMessage("Data acquisition", notes.StatusCode, notes.RawBody);
            }

            var number = FacilityViewRules.ClampPage(referencePage);
            var references = await _acquisition.GetReferenceResourcesForLogAsync(id, LogsRules.ReferencePageSize, number, cancellationToken);
            if (references.StatusCode == StatusCodes.Status204NoContent)
            {
                page.ReferencesPaging = Bar(null, number, LogsRules.ReferencePageSize, 0);
            }
            else if (!references.IsSuccessStatusCode || references.Body is null)
            {
                page.ReferencesError = FacilityFormRules.ServiceMessage("Data acquisition", references.StatusCode, references.RawBody);
            }
            else
            {
                page.ReferencesPaging = Bar(references.Body.Metadata, number, LogsRules.ReferencePageSize, references.Body.Records.Count);
                page.References = references.Body.Records.Select(row => new ReferenceResourceRow
                {
                    ResourceType = row.ResourceType ?? string.Empty,
                    ResourceId = row.ResourceId ?? string.Empty,
                    Phase = row.QueryPhase?.ToString() ?? string.Empty
                }).ToList();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log {LogId} failed", id);
            page.LoadError = "Data acquisition service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<LogsAction> ProcessSelectedAsync(IEnumerable<long>? ids, CancellationToken cancellationToken)
    {
        if (_acquisition is null)
            return Fail(AcquisitionNotConfigured);

        var parsed = LogsRules.ParseIds(ids, out var error);
        if (error is not null)
            return Fail(error);

        try
        {
            var response = parsed.Count == 1
                ? await _acquisition.ProcessAcquisitionLogAsync(parsed[0], cancellationToken)
                : await _acquisition.ProcessAcquisitionLogsBulkAsync(parsed.ToList(), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Fail(FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody));

            return Ok(parsed.Count == 1
                ? "Acquisition log queued."
                : $"Queued {parsed.Count} acquisition logs.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log process failed");
            return Fail("Data acquisition service call failed: " + ex.Message);
        }
    }

    public async Task<LogsAction> ProcessMatchingAsync(AcquisitionQuery? query, CancellationToken cancellationToken)
    {
        var search = LogsRules.Prepare(query, _options.NumericOnlyFacilityId, DateTime.UtcNow);
        if (search.Error is not null)
            return Fail(search.Error);
        if (!search.HasFilter)
            return Fail("Choose at least one filter before processing every matching log.");
        if (_acquisition is null)
            return Fail(AcquisitionNotConfigured);

        try
        {
            var response = await _acquisition.ProcessAcquisitionLogsByFilterAsync(LogsRules.MatchingBody(search), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Fail(FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody));
            return Ok("Matching acquisition logs were queued.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log process-by-filter failed");
            return Fail("Data acquisition service call failed: " + ex.Message);
        }
    }

    public async Task<LogsAction> CancelSelectedAsync(IEnumerable<long>? ids, int minAgeHours, CancellationToken cancellationToken)
    {
        if (_acquisition is null)
            return Fail(AcquisitionNotConfigured);

        var parsed = LogsRules.ParseIds(ids, out var error);
        if (error is not null)
            return Fail(error);

        var age = LogsRules.ClampMinAge(minAgeHours);
        try
        {
            var response = await _acquisition.CancelAcquisitionLogsBulkAsync(parsed.ToList(), age, cancellationToken);
            return CancelMessage(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log cancel failed");
            return Fail("Data acquisition service call failed: " + ex.Message);
        }
    }

    public async Task<LogsAction> CancelMatchingAsync(AcquisitionQuery? query, CancellationToken cancellationToken)
    {
        var search = LogsRules.Prepare(query, _options.NumericOnlyFacilityId, DateTime.UtcNow);
        if (search.Error is not null)
            return Fail(search.Error);
        if (!search.HasFilter)
            return Fail("Choose at least one filter before cancelling every matching log.");
        if (_acquisition is null)
            return Fail(AcquisitionNotConfigured);

        try
        {
            var response = await _acquisition.CancelAcquisitionLogsByFilterAsync(
                LogsRules.MatchingBody(search),
                search.MinAgeHours,
                cancellationToken);
            return CancelMessage(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition log cancel-by-filter failed");
            return Fail("Data acquisition service call failed: " + ex.Message);
        }
    }

    public async Task<LogsAction> ChangeFacilityLogsAsync(string? facilityId, bool restore, CancellationToken cancellationToken)
    {
        if (_acquisition is null)
            return Fail(AcquisitionNotConfigured);

        var id = facilityId?.Trim() ?? string.Empty;
        if (!FacilityFormRules.IsValidFacilityId(id, _options.NumericOnlyFacilityId))
            return Fail(FacilityFormRules.FacilityIdRule(_options.NumericOnlyFacilityId));

        try
        {
            var response = restore
                ? await _acquisition.RestoreLogsByFacilityAsync(id, cancellationToken)
                : await _acquisition.SoftDeleteLogsByFacilityAsync(id, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Fail(FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody));

            return Ok(restore
                ? "Acquisition logs for this facility were restored."
                : "Acquisition logs for this facility were disabled.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facility acquisition log change failed. FacilityId={FacilityId}", id.Sanitize());
            return Fail("Data acquisition service call failed: " + ex.Message);
        }
    }

    public async Task<SftpLogListPage> LoadSftpAsync(SftpQuery? query, CancellationToken cancellationToken)
    {
        var search = LogsRules.PrepareSftp(query, _options.NumericOnlyFacilityId);
        var page = new SftpLogListPage { Query = query ?? new SftpQuery(), Search = search };
        if (search.Error is not null)
        {
            page.LoadError = search.Error;
            return page;
        }

        if (_acquisition is null)
        {
            page.LoadError = AcquisitionNotConfigured;
            return page;
        }

        try
        {
            var response = await _acquisition.SearchSftpAcquisitionLogsAsync(
                search.FacilityId,
                search.Status,
                search.AcquisitionType,
                search.SubType,
                search.Page,
                search.PageSize,
                search.SortBy,
                LogsRules.SortOrder(search.SortDir),
                cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent)
            {
                page.Paging = Bar(null, search.Page, search.PageSize, 0);
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
                return page;
            }

            page.Paging = Bar(response.Body.Metadata, search.Page, search.PageSize, response.Body.Records.Count);
            page.Logs = response.Body.Records
                .Where(row => row.ExternalId is not null)
                .Select(row => new SftpLogRow
                {
                    Id = row.ExternalId!.Value,
                    FacilityId = row.FacilityId ?? string.Empty,
                    AcquisitionType = row.AcquisitionType ?? string.Empty,
                    SubType = row.SubType ?? string.Empty,
                    Status = row.Status ?? string.Empty,
                    Scheduled = FacilityViewRules.When(row.ScheduledDate),
                    Processed = FacilityViewRules.When(row.ProcessDate),
                    RetryAttempts = row.RetryAttempts ?? 0,
                    FileCount = row.FileNames?.Count ?? 0,
                    CanReset = LogsRules.CanResetSftp(row.Status)
                }).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP log search failed");
            page.LoadError = "Data acquisition service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<SftpDetailPage> LoadSftpDetailAsync(string? id, CancellationToken cancellationToken)
    {
        var page = new SftpDetailPage();
        if (!Guid.TryParse(id, out var logId) || logId == Guid.Empty)
        {
            page.NotFound = true;
            page.LoadError = "SFTP log id is not a valid id.";
            return page;
        }

        page.Id = logId;
        if (_acquisition is null)
        {
            page.LoadError = AcquisitionNotConfigured;
            return page;
        }

        try
        {
            var response = await _acquisition.GetSftpLogAsync(logId.ToString(), cancellationToken);
            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                page.NotFound = true;
                page.LoadError = "That SFTP log was not found.";
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody);
                return page;
            }

            var log = response.Body;
            page.FacilityId = log.FacilityId ?? string.Empty;
            page.AcquisitionType = log.AcquisitionType ?? string.Empty;
            page.SubType = log.SubType ?? string.Empty;
            page.Status = log.Status ?? string.Empty;
            page.Scheduled = FacilityViewRules.When(log.ScheduledDate);
            page.Processed = FacilityViewRules.When(log.ProcessDate);
            page.RetryAttempts = log.RetryAttempts ?? 0;
            page.TraceId = log.OriginatingTraceId;
            page.Files = log.FileNames ?? [];
            page.Notes = log.Notes ?? [];
            page.CanReset = LogsRules.CanResetSftp(log.Status);
            page.Benchmarks = (log.Benchmarks ?? []).Select(row => new SftpBenchmarkRow
            {
                Attempt = row.AttemptNumber,
                Started = FacilityViewRules.When(row.AttemptStartedAt),
                Duration = $"{row.TotalDurationMs:0} ms",
                Items = row.ItemsProcessed,
                Succeeded = row.IsSuccessful
            }).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP log {LogId} failed", id.Sanitize());
            page.LoadError = "Data acquisition service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<LogsAction> ResetSftpAsync(string? id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var logId) || logId == Guid.Empty)
            return Fail("SFTP log id is not a valid id.");
        if (_acquisition is null)
            return Fail(AcquisitionNotConfigured);

        try
        {
            var response = await _acquisition.ResetSftpLogAsync(logId.ToString(), cancellationToken);
            if (response.StatusCode == StatusCodes.Status409Conflict)
                return Fail("Only logs in Configuration Required or Max Retries Reached can be reset.");
            if (response.StatusCode == StatusCodes.Status404NotFound)
                return Fail("That SFTP log was not found.");
            if (!response.IsSuccessStatusCode)
                return Fail(FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody));
            return Ok("SFTP log reset for retry.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SFTP log reset failed. LogId={LogId}", id.Sanitize());
            return Fail("Data acquisition service call failed: " + ex.Message);
        }
    }

    public async Task<AuditListPage> LoadAuditAsync(AuditQuery? query, CancellationToken cancellationToken)
    {
        var search = LogsRules.PrepareAudit(query, _options.NumericOnlyFacilityId);
        var page = new AuditListPage { Query = query ?? new AuditQuery(), Search = search };
        if (search.Error is not null)
        {
            page.LoadError = search.Error;
            return page;
        }

        if (_audit is null)
        {
            page.LoadError = AuditNotConfigured;
            return page;
        }

        try
        {
            var response = await _audit.SearchAsync(
                search.SearchText,
                search.FacilityId,
                search.CorrelationId,
                search.Service,
                search.Action,
                search.User,
                search.SortBy,
                LogsRules.SortOrder(search.SortDir),
                search.Page,
                search.PageSize,
                cancellationToken);
            if (response.StatusCode == StatusCodes.Status204NoContent)
            {
                page.Paging = Bar(null, search.Page, search.PageSize, 0);
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Audit", response.StatusCode, response.RawBody);
                return page;
            }

            page.Paging = Bar(response.Body.Metadata, search.Page, search.PageSize, response.Body.Records.Count);
            page.Events = response.Body.Records.Select(row =>
            {
                var id = row.Id ?? string.Empty;
                return new AuditEventRow
                {
                    Id = id,
                    Link = Guid.TryParse(id, out var parsed) && parsed != Guid.Empty,
                    FacilityId = row.FacilityId ?? string.Empty,
                    CorrelationId = row.CorrelationId ?? string.Empty,
                    Service = row.ServiceName ?? string.Empty,
                    Action = row.Action ?? string.Empty,
                    User = row.User ?? string.Empty,
                    When = FacilityViewRules.When(row.EventDate),
                    Resource = row.Resource ?? string.Empty
                };
            }).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit search failed");
            page.LoadError = "Audit service call failed: " + ex.Message;
        }

        return page;
    }

    public async Task<AuditDetailPage> LoadAuditDetailAsync(string? id, CancellationToken cancellationToken)
    {
        var page = new AuditDetailPage { Id = id?.Trim() ?? string.Empty };
        if (!Guid.TryParse(page.Id, out var auditId) || auditId == Guid.Empty)
        {
            page.NotFound = true;
            page.LoadError = "Audit id is not a valid id.";
            return page;
        }

        if (_audit is null)
        {
            page.LoadError = AuditNotConfigured;
            return page;
        }

        try
        {
            var response = await _audit.GetAsync(auditId, cancellationToken);
            if (response.StatusCode == StatusCodes.Status404NotFound)
            {
                page.NotFound = true;
                page.LoadError = "That audit event was not found.";
                return page;
            }

            if (!response.IsSuccessStatusCode || response.Body is null)
            {
                page.LoadError = FacilityFormRules.ServiceMessage("Audit", response.StatusCode, response.RawBody);
                return page;
            }

            var row = response.Body;
            page.Id = auditId.ToString();
            page.FacilityId = row.FacilityId ?? string.Empty;
            page.CorrelationId = row.CorrelationId ?? string.Empty;
            page.Service = row.ServiceName ?? string.Empty;
            page.Action = row.Action ?? string.Empty;
            page.User = row.User ?? string.Empty;
            page.When = FacilityViewRules.When(row.EventDate);
            page.Resource = row.Resource ?? string.Empty;
            page.Notes = row.Notes ?? string.Empty;
            page.Changes = (row.PropertyChanges ?? []).Select(change => new AuditChangeRow
            {
                Name = change.PropertyName ?? string.Empty,
                Before = change.InitialPropertyValue ?? string.Empty,
                After = change.NewPropertyValue ?? string.Empty
            }).ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audit event {AuditId} failed", page.Id.Sanitize());
            page.LoadError = "Audit service call failed: " + ex.Message;
        }

        return page;
    }

    private async Task<string?> CountsAsync(AcquisitionLogListPage page, AcquisitionSearch search, CancellationToken cancellationToken)
    {
        try
        {
            var counts = await _acquisition!.GetReportStatusCountsAsync(search.ReportId!, cancellationToken, search.PatientId);
            if (!counts.IsSuccessStatusCode || counts.Body is null)
                return FacilityFormRules.ServiceMessage("Data acquisition", counts.StatusCode, counts.RawBody);

            page.Counts = counts.Body.Statuses
                .Select(row => new StatusCountRow { Name = row.Name ?? string.Empty, Count = row.Count })
                .ToList();
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Acquisition status counts failed. ReportId={ReportId}", search.ReportId.Sanitize());
            return "Status counts could not be loaded: " + ex.Message;
        }
    }

    public async Task<AcquisitionLogListPage> LoadAcquisitionForFacilitiesAsync(
        AcquisitionQuery query,
        IReadOnlyList<string> facilityIds,
        bool truncated,
        CancellationToken cancellationToken)
    {
        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var search = LogsRules.Prepare(query, _options.NumericOnlyFacilityId, DateTime.UtcNow);
        var page = new AcquisitionLogListPage { Query = query, Search = search };
        if (search.Error is not null)
        {
            page.LoadError = search.Error;
            return page;
        }

        var cached = await AutomationFacilitySearch.CachedAsync(
            _cache,
            "acquisition",
            query.AutomationFingerprint(),
            facilityIds,
            async (facilityId, token) =>
            {
                var one = await LoadAcquisitionAsync(
                    query.ForFacility(facilityId, 1, LogsRules.PageSizes[^1]),
                    token);
                return new FacilitySearchPage<AcquisitionListRow>(one.Logs, one.Paging.TotalCount, one.LoadError);
            },
            cancellationToken);

        var descending = search.SortDir != "asc";
        var ordered = OrderAcquisition(cached.Rows, search.SortBy, descending);
        var slice = AutomationMarkRules.Slice(ordered, search.Page, search.PageSize);
        page.Logs = slice.Items;
        page.Paging = new PageBar
        {
            Page = slice.Page,
            PageSize = slice.Size,
            TotalCount = slice.Total,
            TotalPages = slice.Pages
        };
        page.LoadError = cached.Error;
        page.ScopeNote = AutomationMarkRules.SearchNote(truncated, cached.Partial);
        return page;
    }

    public async Task<SftpLogListPage> LoadSftpForFacilitiesAsync(
        SftpQuery query,
        IReadOnlyList<string> facilityIds,
        bool truncated,
        CancellationToken cancellationToken)
    {
        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var search = LogsRules.PrepareSftp(query, _options.NumericOnlyFacilityId);
        var page = new SftpLogListPage { Query = query, Search = search };
        if (search.Error is not null)
        {
            page.LoadError = search.Error;
            return page;
        }

        var cached = await AutomationFacilitySearch.CachedAsync(
            _cache,
            "sftp",
            query.AutomationFingerprint(),
            facilityIds,
            async (facilityId, token) =>
            {
                var one = await LoadSftpAsync(query.ForFacility(facilityId, 1, LogsRules.PageSizes[^1]), token);
                return new FacilitySearchPage<SftpLogRow>(one.Logs, one.Paging.TotalCount, one.LoadError);
            },
            cancellationToken);

        var slice = AutomationMarkRules.Slice(cached.Rows, search.Page, search.PageSize);
        page.Logs = slice.Items;
        page.Paging = new PageBar
        {
            Page = slice.Page,
            PageSize = slice.Size,
            TotalCount = slice.Total,
            TotalPages = slice.Pages
        };
        page.LoadError = cached.Error;
        page.ScopeNote = AutomationMarkRules.SearchNote(truncated, cached.Partial);
        return page;
    }

    public async Task<AuditListPage> LoadAuditForFacilitiesAsync(
        AuditQuery query,
        IReadOnlyList<string> facilityIds,
        bool truncated,
        CancellationToken cancellationToken)
    {
        query.Scope = AutomationMarkRules.NormalizeScope(query.Scope);
        var search = LogsRules.PrepareAudit(query, _options.NumericOnlyFacilityId);
        var page = new AuditListPage { Query = query, Search = search };
        if (search.Error is not null)
        {
            page.LoadError = search.Error;
            return page;
        }

        var cached = await AutomationFacilitySearch.CachedAsync(
            _cache,
            "audit",
            query.AutomationFingerprint(),
            facilityIds,
            async (facilityId, token) =>
            {
                var one = await LoadAuditAsync(
                    query.ForFacility(facilityId, 1, LogsRules.AuditPageSizes[^1]),
                    token);
                return new FacilitySearchPage<AuditEventRow>(one.Events, one.Paging.TotalCount, one.LoadError);
            },
            cancellationToken);

        var slice = AutomationMarkRules.Slice(cached.Rows, search.Page, search.PageSize);
        page.Events = slice.Items;
        page.Paging = new PageBar
        {
            Page = slice.Page,
            PageSize = slice.Size,
            TotalCount = slice.Total,
            TotalPages = slice.Pages
        };
        page.LoadError = cached.Error;
        page.ScopeNote = AutomationMarkRules.SearchNote(truncated, cached.Partial);
        return page;
    }

    private static List<AcquisitionListRow> OrderAcquisition(
        IReadOnlyList<AcquisitionListRow> rows,
        string sortBy,
        bool descending)
    {
        return sortBy switch
        {
            "FacilityId" => SortText(rows, row => row.FacilityId, descending),
            "PatientId" => SortText(rows, row => row.PatientId, descending),
            "QueryType" => SortText(rows, row => row.QueryType, descending),
            "QueryPhase" => SortText(rows, row => row.Phase, descending),
            "Status" => SortText(rows, row => row.Status, descending),
            "Priority" => SortText(rows, row => row.Priority, descending),
            "Id" => (descending ? rows.OrderByDescending(row => row.Id) : rows.OrderBy(row => row.Id)).ToList(),
            _ => (descending
                ? rows.OrderByDescending(row => row.CreatedUtc ?? DateTime.MinValue).ThenByDescending(row => row.Id)
                : rows.OrderBy(row => row.CreatedUtc ?? DateTime.MinValue).ThenBy(row => row.Id)).ToList()
        };
    }

    private static List<AcquisitionListRow> SortText(
        IReadOnlyList<AcquisitionListRow> rows,
        Func<AcquisitionListRow, string> key,
        bool descending)
    {
        var ordered = descending
            ? rows.OrderByDescending(key, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(key, StringComparer.OrdinalIgnoreCase);
        return ordered.ToList();
    }

    private static AcquisitionLogQuery ToQuery(AcquisitionSearch search) => new()
    {
        FacilityId = search.FacilityId,
        ReportId = search.ReportId,
        PatientId = search.PatientId,
        ResourceId = search.ResourceId,
        ResourceType = search.ResourceType,
        QueryPhase = search.QueryPhase,
        QueryType = search.QueryType,
        Statuses = search.Statuses,
        Priority = search.Priority,
        IncludeDeleted = search.IncludeDeleted,
        CreatedBefore = search.CreatedBefore,
        PageNumber = search.Page,
        PageSize = search.PageSize,
        SortBy = search.SortBy,
        SortOrder = LogsRules.SortOrder(search.SortDir),
        SearchTerm = search.SearchTerm
    };

    private static AcquisitionListRow MapRow(DataAcquisitionLogSummaryApiModel row)
    {
        var status = row.Status?.ToString() ?? string.Empty;
        return new AcquisitionListRow
        {
            Id = row.Id,
            FacilityId = row.FacilityId ?? string.Empty,
            PatientId = row.PatientId ?? string.Empty,
            ReportId = row.ReportTrackingId,
            ReportLink = ReportLink(row.FacilityId, row.ReportTrackingId),
            Status = status,
            Phase = row.QueryPhase?.ToString() ?? string.Empty,
            QueryType = row.QueryType?.ToString() ?? string.Empty,
            Priority = row.Priority ?? string.Empty,
            Created = FacilityViewRules.When(row.CreateDate),
            CreatedUtc = row.CreateDate,
            Resources = Join(row.ResourceTypes),
            ResourceId = row.ResourceId ?? string.Empty,
            Deleted = row.IsDeleted,
            CanProcess = LogsRules.CanProcess(status)
        };
    }

    private static bool ReportLink(string? facilityId, string? reportId) =>
        !string.IsNullOrWhiteSpace(facilityId)
        && Guid.TryParse(reportId, out var id)
        && id != Guid.Empty;

    private static LogsAction CancelMessage(LinkApiResponse<LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.DataAcquisitionBulkActionResultApiModel> response)
    {
        if (!response.IsSuccessStatusCode || response.Body is null)
            return Fail(FacilityFormRules.ServiceMessage("Data acquisition", response.StatusCode, response.RawBody));

        var cancelled = response.Body.Cancelled;
        var requested = response.Body.Requested;
        var ineligible = response.Body.Ineligible;
        if (cancelled == 0)
            return Fail($"No logs were cancelled. {ineligible} were not eligible.");
        if (ineligible > 0)
            return Ok($"Cancelled {cancelled} of {requested} logs. {ineligible} were not eligible.");
        return Ok(cancelled == 1 ? "Cancelled 1 log." : $"Cancelled {cancelled} logs.");
    }

    private static string Join(IEnumerable<string>? values)
    {
        if (values is null)
            return "—";
        var text = string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value)));
        return text.Length == 0 ? "—" : text;
    }

    private static PageBar Bar(PaginationMetadata? metadata, int page, int pageSize, int recordCount)
    {
        if (metadata is null)
        {
            return new PageBar
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = recordCount,
                TotalPages = recordCount == 0 ? 0 : 1
            };
        }

        return new PageBar
        {
            Page = metadata.PageNumber < 1 ? page : metadata.PageNumber,
            PageSize = metadata.PageSize < 1 ? pageSize : metadata.PageSize,
            TotalCount = metadata.TotalCount,
            TotalPages = metadata.TotalPages
        };
    }

    private static LogsAction Ok(string message) => new(true, message);
    private static LogsAction Fail(string message) => new(false, message);
    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);
}
