using Link.UI.Models;
using Microsoft.AspNetCore.Http;

namespace Link.UI.Services;

/// <summary>
/// Home overview decisions that do not call a service: facility split, top-N rows,
/// and a card that fails on its own.
/// </summary>
public static class HomeOverviewRules
{
    public const int RowLimit = 5;
    public const int CacheSeconds = 15;
    public static readonly TimeSpan CardBudget = TimeSpan.FromSeconds(6);

    public static readonly string[] ErrorLogStatuses = ["Failed", "MaxRetriesReached"];

    public enum CardFailure
    {
        Timeout,
        Error
    }

    public static async Task<T> LoadCardAsync<T>(
        Func<CancellationToken, Task<T>> load,
        Func<CardFailure, T> failed,
        CancellationToken cancellationToken,
        TimeSpan budget,
        Action<Exception>? onError = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget);
        try
        {
            return await load(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return failed(CardFailure.Timeout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            return failed(CardFailure.Error);
        }
    }

    public static bool TryReadFacilities(
        int statusCode,
        bool success,
        IReadOnlyDictionary<string, string>? body,
        out IReadOnlyCollection<string> ids)
    {
        if (statusCode == StatusCodes.Status204NoContent || (success && body is { Count: 0 }))
        {
            ids = [];
            return true;
        }

        if (!success || body is null)
        {
            ids = [];
            return false;
        }

        ids = body.Keys.Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        return true;
    }

    public static FacilityCard Facilities(
        bool listReachable,
        string? listMessage,
        bool ownershipReachable,
        IEnumerable<string>? facilityIds,
        AutomationOwnershipIndex? ownership,
        bool classify = true)
    {
        if (!listReachable)
        {
            return new FacilityCard
            {
                Message = string.IsNullOrWhiteSpace(listMessage)
                    ? "Tenant service could not be reached."
                    : listMessage
            };
        }

        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in facilityIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(id))
                distinct.Add(id.Trim());
        }

        int? automation = null;
        int? regular = null;
        if (classify && ownershipReachable && ownership is not null)
        {
            var owned = distinct.Count(ownership.Contains);
            automation = owned;
            regular = distinct.Count - owned;
        }

        return new FacilityCard
        {
            Reachable = true,
            Total = distinct.Count,
            Automation = automation,
            Regular = regular,
            Message = classify && !ownershipReachable ? "Automation ownership could not be read." : null
        };
    }

    public static ReportCard Reports(
        bool reachable,
        string? message,
        long total,
        IEnumerable<FacilityReportRow>? rows,
        AutomationOwnershipIndex? ownership = null)
    {
        if (!reachable)
        {
            return new ReportCard
            {
                Message = string.IsNullOrWhiteSpace(message) ? "Report service could not be reached." : message
            };
        }

        return new ReportCard
        {
            Reachable = true,
            Total = total < 0 ? 0 : total,
            Rows = (rows ?? []).Take(RowLimit).Select(row => new HomeReportLine
            {
                Id = row.Id.ToString(),
                FacilityId = row.FacilityId,
                Status = row.StatusLabel,
                Badge = FacilityViewRules.StatusBadge(row.Status),
                When = row.Created,
                AutomationRunId = ownership?.RunIdFor(row.FacilityId)
            }).ToList()
        };
    }

    public static bool IsHealthyStatus(string? status)
    {
        var value = status?.Trim().ToLowerInvariant();
        return value is "healthy" or "ok";
    }

    public static HealthCard Health(
        bool reachable,
        string? message,
        IEnumerable<(string Service, string Status)>? rows,
        bool apiReachable,
        string? apiMessage,
        Guid? apiRunId,
        string? apiMode,
        string? apiService,
        string? apiWhen)
    {
        var listed = (rows ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(row.Service))
            .Select(row => (Service: row.Service.Trim(), Status: row.Status ?? ""))
            .ToList();
        var unhealthy = listed.Where(row => !IsHealthyStatus(row.Status)).Select(row => row.Service).ToList();
        return new HealthCard
        {
            Reachable = reachable,
            Message = reachable
                ? null
                : string.IsNullOrWhiteSpace(message) ? "Admin.BFF could not be reached." : message,
            Healthy = reachable ? listed.Count - unhealthy.Count : 0,
            Unhealthy = reachable ? unhealthy.Count : 0,
            UnhealthyNames = reachable ? unhealthy.Take(4).ToList() : [],
            ApiHealthReachable = apiReachable,
            ApiHealthText = ApiHealthText(apiReachable, apiMessage, apiRunId, apiMode, apiService, apiWhen)
        };
    }

    public static string ApiHealthText(
        bool reachable,
        string? message,
        Guid? runId,
        string? mode,
        string? service,
        string? when)
    {
        if (!reachable)
            return string.IsNullOrWhiteSpace(message) ? "API health storage could not be read." : message;
        if (runId is null || runId == Guid.Empty)
            return "No API health run yet.";

        var scope = string.Equals(mode, "All", StringComparison.OrdinalIgnoreCase)
            ? "all services"
            : string.IsNullOrWhiteSpace(service) ? "one service" : service.Trim();
        var at = string.IsNullOrWhiteSpace(when) ? "" : " · " + when.Trim();
        return "Latest API health run: " + scope + at;
    }

    public static RunCard Runs(bool reachable, string? message, int activeCount, IEnumerable<AutomationRunRow>? active, IEnumerable<AutomationRunRow>? recent)
    {
        if (!reachable)
        {
            return new RunCard
            {
                Message = string.IsNullOrWhiteSpace(message)
                    ? AutomationRunReader.UnreachableMessage
                    : message
            };
        }

        var activeLines = Lines(active);
        var activeIds = new HashSet<string>(activeLines.Select(line => line.Id), StringComparer.OrdinalIgnoreCase);
        return new RunCard
        {
            Reachable = true,
            ActiveCount = Math.Max(0, activeCount),
            Active = activeLines,
            Recent = Lines(recent, activeIds)
        };
    }

    public static LogCard Logs(
        bool acquisitionReachable,
        string? acquisitionMessage,
        long total,
        IEnumerable<HomeLogLine>? rows,
        bool auditReachable,
        string? auditMessage)
    {
        return new LogCard
        {
            Reachable = acquisitionReachable,
            Message = acquisitionReachable
                ? null
                : string.IsNullOrWhiteSpace(acquisitionMessage)
                    ? "Data acquisition could not be reached."
                    : acquisitionMessage,
            Total = acquisitionReachable && total > 0 ? total : 0,
            Rows = acquisitionReachable ? (rows ?? []).Take(RowLimit).ToList() : [],
            AuditMessage = auditReachable
                ? null
                : string.IsNullOrWhiteSpace(auditMessage) ? "Audit could not be reached." : auditMessage
        };
    }

    public static string RunBadge(string? status)
    {
        if (status == "Succeeded")
            return "au-badge-success";
        if (status == "Failed")
            return "au-badge-danger";
        if (status == "Cancelled")
            return "au-badge-warning";
        if (AutomationRules.IsInProgress(status))
            return "au-badge-active";
        return "au-badge-muted";
    }

    public static string LogBadge(string? status) =>
        status is "Failed" or "MaxRetriesReached" ? "au-badge-danger" : "au-badge-muted";

    public static string When(DateTimeOffset value) =>
        value == default ? "" : LinkUiTime.IsoUtc(value);

    private static IReadOnlyList<HomeRunLine> Lines(IEnumerable<AutomationRunRow>? rows, HashSet<string>? skip = null) =>
        (rows ?? [])
            .Where(row => skip is null || !skip.Contains(row.RunId.ToString()))
            .Take(RowLimit)
            .Select(row => new HomeRunLine
        {
            Id = row.RunId.ToString(),
            Name = string.IsNullOrWhiteSpace(row.RunName) ? row.RunId.ToString() : row.RunName,
            Status = string.IsNullOrWhiteSpace(row.StatusLabel) ? AutomationRules.StatusLabel(row.Status) : row.StatusLabel,
            Badge = RunBadge(row.Status),
            When = When(row.CreatedAt)
        }).ToList();
}
