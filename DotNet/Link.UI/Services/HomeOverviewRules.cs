using System.Globalization;
using Automation.UI.Models.ApiHealth;
using LantanaGroup.Link.Shared.Application.Models;
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
    public const int TrendLength = 7;
    public const int StuckHours = 24;
    public const double SlowApiMs = 2000;
    public const int IssueLimit = 8;
    public const int ChipLimit = 8;

    public const string ApiHealthScenarioName = "ApiHealthScenario";
    public static readonly TimeSpan CardBudget = TimeSpan.FromSeconds(6);

    public static readonly string[] ErrorLogStatuses = ["Failed", "MaxRetriesReached"];
    public static readonly string[] InFlightStatuses = ["New", "Scheduled", "EndOfPeriod"];
    public const string SubmittedStatus = "Submitted";
    public const string CompletedStatus = "CompletedNotSubmitted";

    public const string InFlightHref = "/Reports?status=New,Scheduled,EndOfPeriod";
    public const string SubmittedHref = "/Reports?status=Submitted";
    public const string CompletedHref = "/Reports?status=CompletedNotSubmitted";
    public const string FailedLogsHref = "/Logs/Acquisition?status=Failed,MaxRetriesReached";
    public const string HealthHref = "/System/Health";
    public const string MetricsHref = "/Metrics";
    public const string TenantsHref = "/Tenants";
    public const string ReportsHref = "/Reports";
    public const string AuditHref = "/Logs/Audit";
    public const string AutomationTenantsHref = "/Tenants?scope=automation";
    public const string AutomationHref = "/Automation";

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

    /// <summary>
    /// Facility tile from a count endpoint. The id set never enters the card.
    /// </summary>
    public static FacilityCard FacilitiesFromCounts(
        bool reachable,
        string? message,
        int total,
        int? matched,
        bool ownershipReachable,
        bool classify)
    {
        if (!reachable)
        {
            return new FacilityCard
            {
                Message = string.IsNullOrWhiteSpace(message)
                    ? "Tenant service could not be reached."
                    : message
            };
        }

        int? automation = null;
        int? regular = null;
        string? note = null;
        if (classify && ownershipReachable && matched is int owned)
        {
            automation = Math.Max(0, owned);
            regular = Math.Max(0, total - automation.Value);
        }
        else if (classify && !ownershipReachable)
        {
            note = "Automation ownership could not be read.";
        }

        return new FacilityCard
        {
            Reachable = true,
            Total = Math.Max(0, total),
            Automation = automation,
            Regular = regular,
            Message = note
        };
    }

    public static IReadOnlyList<IReadOnlyList<string>> FacilityIdBatches(IReadOnlyCollection<string> ids)
    {
        if (ids.Count == 0)
            return [[]];

        var list = ids as IReadOnlyList<string> ?? ids.ToList();
        var batches = new List<IReadOnlyList<string>>();
        for (var index = 0; index < list.Count; index += AggregateCountLimits.MaxFacilityIds)
        {
            var count = Math.Min(AggregateCountLimits.MaxFacilityIds, list.Count - index);
            var batch = new string[count];
            for (var offset = 0; offset < count; offset++)
                batch[offset] = list[index + offset];
            batches.Add(batch);
        }

        return batches;
    }

    public static ReportCard Reports(
        bool reachable,
        string? message,
        long total,
        IEnumerable<FacilityReportRow>? rows,
        IReadOnlySet<string>? testFacilityIds = null)
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
                IsTest = testFacilityIds is not null
                    && !string.IsNullOrWhiteSpace(row.FacilityId)
                    && testFacilityIds.Contains(row.FacilityId)
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

    public sealed record ApiHealthSighting(Guid RunId, string Mode, string? Service, DateTimeOffset At);

    /// <summary>
    /// The dashboard chip follows the newest API health activity. A page start,
    /// an execution that has finished, and an ApiHealthScenario automation run
    /// are the same kind of run.
    /// </summary>
    public static ApiHealthSighting? ChooseLatestApiHealth(
        ApiHealthLatestRunContext? stored,
        ApiHealthExecutionRunStatus? execution,
        AutomationRunRow? scenario)
    {
        ApiHealthSighting? best = null;

        void Consider(Guid id, string? mode, string? service, DateTimeOffset at)
        {
            if (id == Guid.Empty || at == default)
                return;

            if (best is null || at > best.At)
                best = new ApiHealthSighting(id, NormalizeApiMode(mode), service, at);
        }

        if (stored is not null)
            Consider(stored.RunId, stored.RunMode, stored.ServiceName, stored.StartedAt);

        if (execution is not null)
        {
            var at = execution.StartedAt;
            if (execution.FinishedAt is DateTimeOffset finished && finished > at)
                at = finished;
            Consider(execution.RunId, execution.RunMode, execution.ServiceName, at);
        }

        if (scenario is not null)
        {
            var at = scenario.CreatedAt;
            if (scenario.StartedAt is DateTimeOffset started && started > at)
                at = started;
            if (scenario.FinishedAt is DateTimeOffset finished && finished > at)
                at = finished;

            var mode = "All";
            string? service = null;
            if (execution is not null && execution.SeedRunId == scenario.RunId)
            {
                mode = execution.RunMode;
                service = execution.ServiceName;
            }

            Consider(scenario.RunId, mode, service, at);
        }

        return best;
    }

    private static string NormalizeApiMode(string? mode) =>
        string.Equals(mode, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Single";

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
        string? auditMessage,
        bool acquisitionTrendReachable = true,
        IReadOnlyList<TrendDay>? acquisitionTrend = null,
        bool auditCounted = true,
        long auditErrors = 0)
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
            TrendReachable = acquisitionReachable && acquisitionTrendReachable,
            Trend = acquisitionReachable && acquisitionTrendReachable
                ? (acquisitionTrend ?? []).Take(TrendLength).ToList()
                : [],
            AuditMessage = auditReachable
                ? null
                : string.IsNullOrWhiteSpace(auditMessage) ? "Audit could not be reached." : auditMessage,
            AuditCounted = auditReachable && auditCounted,
            AuditErrors = auditReachable && auditCounted ? Math.Max(0, auditErrors) : 0
        };
    }

    public static string RunBadge(string? status) => StatusPills.ForRun(status);

    public static string LogBadge(string? status) => StatusPills.ForLog(status);

    public static string When(DateTimeOffset value) =>
        value == default ? "" : LinkUiTime.Display(value);

    public static DateTimeOffset? ParseIso(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    public static IReadOnlyList<DateOnly> TrendDays(DateOnly todayUtc)
    {
        var days = new DateOnly[TrendLength];
        for (var i = 0; i < TrendLength; i++)
            days[i] = todayUtc.AddDays(i - (TrendLength - 1));
        return days;
    }

    public static string CreatedHref(string day) => "/Reports?created=" + day;

    public static string CpuText(double? percent) =>
        percent is double value ? Math.Round(value).ToString(CultureInfo.InvariantCulture) + "%" : "—";

    public static string LatencyText(double? milliseconds) =>
        milliseconds is double value ? Math.Round(value).ToString(CultureInfo.InvariantCulture) + " ms" : "—";

    public static int BarPercent(long count, long max)
    {
        if (max <= 0 || count <= 0)
            return 0;
        return (int)Math.Clamp(Math.Round(count * 100.0 / max), 4, 100);
    }

    /// <summary>
    /// Real-facility count when automation is on and the split is known.
    /// When automation is off the home page does not classify, so the tile is the total.
    /// </summary>
    public static string PrimaryFacilityText(FacilityCard card, bool automationVisible)
    {
        if (!card.Reachable)
            return "—";
        if (automationVisible && card.Regular is int real)
            return real.ToString(CultureInfo.InvariantCulture);
        return card.Total.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The tile says "Real facilities" only when the number is the real-facility split.
    /// A total, including the unclassified count used when automation is off, stays "Facilities".
    /// </summary>
    public static string PrimaryFacilityLabel(FacilityCard card, bool automationVisible) =>
        card.Reachable && automationVisible && card.Regular is int ? "Real facilities" : "Facilities";

    /// <summary>
    /// Scope query for the real-facilities tile. Set only when that tile's number is the real split.
    /// </summary>
    public static string? RealFacilityScope(bool automationVisible, int? regular) =>
        automationVisible && regular is int ? AutomationMarkRules.Real : null;

    public static ActivityCard Activity(
        bool inFlightReachable,
        long inFlight,
        DateTimeOffset? oldestInFlightUtc,
        bool submittedReachable,
        long submitted,
        bool completedReachable,
        long completedNotSubmitted,
        IReadOnlyList<TrendDay>? trend) => new()
    {
        InFlightReachable = inFlightReachable,
        InFlight = inFlightReachable ? Math.Max(0, inFlight) : 0,
        OldestInFlightUtc = inFlightReachable ? oldestInFlightUtc : null,
        SubmittedReachable = submittedReachable,
        Submitted = submittedReachable ? Math.Max(0, submitted) : 0,
        CompletedReachable = completedReachable,
        CompletedNotSubmitted = completedReachable ? Math.Max(0, completedNotSubmitted) : 0,
        Trend = (trend ?? []).Take(TrendLength).ToList()
    };

    public static ServicePulseCard Pulse(bool reachable, string? message, IEnumerable<PulseSample>? samples)
    {
        if (!reachable)
        {
            return new ServicePulseCard
            {
                Message = string.IsNullOrWhiteSpace(message) ? "Service metrics could not be read." : message
            };
        }

        var chips = (samples ?? [])
            .Where(sample => string.Equals(sample.Group, "pipeline", StringComparison.OrdinalIgnoreCase))
            .Where(sample => !string.IsNullOrWhiteSpace(sample.Name))
            .Take(ChipLimit)
            .Select(sample => new ServiceChip
            {
                Name = sample.Name.Trim(),
                CpuPercent = sample.CpuPercent,
                ApiP95Ms = sample.ApiP95Ms
            })
            .ToList();
        return new ServicePulseCard { Reachable = true, Chips = chips };
    }

    public static IReadOnlyList<HomeIssue> Issues(
        bool facilitiesReachable,
        bool ownershipKnown,
        bool automationVisible,
        ActivityCard activity,
        HealthCard health,
        LogCard logs,
        ServicePulseCard pulse,
        DateTimeOffset utcNow)
    {
        var issues = new List<HomeIssue>();
        if (!facilitiesReachable)
        {
            issues.Add(new HomeIssue
            {
                Title = "Facilities could not be read",
                Detail = "The tenant list did not answer.",
                Href = TenantsHref
            });
        }
        else if (automationVisible && !ownershipKnown)
        {
            issues.Add(new HomeIssue
            {
                Title = "Automation ownership could not be read",
                Detail = "Owned facilities could not be separated.",
                Href = AutomationTenantsHref
            });
        }

        if (!activity.InFlightReachable || !activity.SubmittedReachable || !activity.CompletedReachable)
        {
            issues.Add(new HomeIssue
            {
                Title = "Report counts could not be read",
                Detail = "Status totals did not answer.",
                Href = ReportsHref
            });
        }

        if (activity.InFlightReachable
            && activity.OldestInFlightUtc is DateTimeOffset oldest
            && utcNow - oldest >= TimeSpan.FromHours(StuckHours))
        {
            issues.Add(new HomeIssue
            {
                Title = "A report has been in flight for more than a day",
                Detail = LinkUiTime.Display(oldest),
                Href = InFlightHref
            });
        }

        if (activity.Trend.Count > 0 && activity.Trend.All(day => !day.Reachable))
        {
            issues.Add(new HomeIssue
            {
                Title = "Report trend could not be read",
                Detail = "Daily counts did not answer.",
                Href = ReportsHref
            });
        }

        if (!health.Reachable)
        {
            issues.Add(new HomeIssue
            {
                Title = "Service health could not be read",
                Detail = string.IsNullOrWhiteSpace(health.Message) ? "Admin.BFF did not answer." : health.Message,
                Href = HealthHref
            });
        }
        else if (health.Unhealthy > 0)
        {
            issues.Add(new HomeIssue
            {
                Title = health.Unhealthy.ToString(CultureInfo.InvariantCulture) + " unhealthy services",
                Detail = string.Join(", ", health.UnhealthyNames),
                Href = HealthHref
            });
        }

        if (!logs.Reachable)
        {
            issues.Add(new HomeIssue
            {
                Title = "Acquisition logs could not be read",
                Detail = string.IsNullOrWhiteSpace(logs.Message) ? "Data acquisition did not answer." : logs.Message,
                Href = FailedLogsHref
            });
        }
        else if (logs.Total > 0)
        {
            issues.Add(new HomeIssue
            {
                Title = logs.Total.ToString(CultureInfo.InvariantCulture) + " failed acquisition logs",
                Detail = "Failed or out of retries.",
                Href = FailedLogsHref
            });
        }

        if (!string.IsNullOrWhiteSpace(logs.AuditMessage))
        {
            issues.Add(new HomeIssue
            {
                Title = "Audit could not be read",
                Detail = logs.AuditMessage,
                Href = AuditHref
            });
        }
        else if (logs.AuditCounted && logs.AuditErrors > 0)
        {
            issues.Add(new HomeIssue
            {
                Title = logs.AuditErrors.ToString(CultureInfo.InvariantCulture) + " audit errors in the last 24 hours",
                Detail = "Notes in that window record a failure.",
                Href = AuditHref
            });
        }

        if (automationVisible && !pulse.Reachable)
        {
            issues.Add(new HomeIssue
            {
                Title = "Service metrics could not be read",
                Detail = string.IsNullOrWhiteSpace(pulse.Message) ? "Service metrics could not be read." : pulse.Message,
                Href = MetricsHref
            });
        }
        else if (automationVisible)
        {
            var slow = pulse.Chips
                .Where(chip => chip.ApiP95Ms is > SlowApiMs)
                .OrderByDescending(chip => chip.ApiP95Ms)
                .FirstOrDefault();
            if (slow is not null)
            {
                issues.Add(new HomeIssue
                {
                    Title = slow.Name + " API is slower than 2 seconds",
                    Detail = LatencyText(slow.ApiP95Ms),
                    Href = MetricsHref
                });
            }
        }

        return issues.Take(IssueLimit).ToList();
    }

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
