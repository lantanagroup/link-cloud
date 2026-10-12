using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Integration.Normalization;
using LantanaGroup.Link.Shared.Application.Models.Integration.Report;
using Link.UI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Link.UI.Services;

/// <summary>
/// The report page's progress: pipeline stage counts, submission breakdown, patients per measure and timing.
/// Every call is a per-report aggregate (no per-patient reads), so it is safe to poll at national scale.
/// </summary>
public sealed class ReportProgressService
{
    public const int LiveIntervalMs = 5000;
    private static readonly TimeSpan PopulationCacheLive = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PopulationCacheFinal = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RecentActivity = TimeSpan.FromMinutes(2);

    private readonly IReportServiceClient? _reports;
    private readonly IDataAcquisitionServiceClient? _acquisition;
    private readonly INormalizationServiceClient? _normalization;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ReportProgressService> _logger;

    public ReportProgressService(
        IReportServiceClient? reports,
        IDataAcquisitionServiceClient? acquisition,
        INormalizationServiceClient? normalization,
        IMemoryCache cache,
        ILogger<ReportProgressService> logger)
    {
        _reports = reports;
        _acquisition = acquisition;
        _normalization = normalization;
        _cache = cache;
        _logger = logger;
    }

    public static ReportProgressService Create(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IOptions<ServiceRegistry>>().Value;
        return new ReportProgressService(
            string.IsNullOrWhiteSpace(registry.ReportServiceUrl) ? null : services.GetRequiredService<IReportServiceClient>(),
            string.IsNullOrWhiteSpace(registry.DataAcquisitionServiceUrl) ? null : services.GetRequiredService<IDataAcquisitionServiceClient>(),
            string.IsNullOrWhiteSpace(registry.NormalizationServiceUrl) ? null : services.GetRequiredService<INormalizationServiceClient>(),
            services.GetRequiredService<IMemoryCache>(),
            services.GetRequiredService<ILogger<ReportProgressService>>());
    }

    public static bool IsTerminal(ScheduleStatus status) =>
        status is ScheduleStatus.Submitted or ScheduleStatus.CompletedNotSubmitted;

    /// <summary>Validation results keep landing for a short while after submission, so the validation page stays live that long.</summary>
    public static readonly TimeSpan ValidationGrace = TimeSpan.FromMinutes(3);

    public static bool ValidationSettled(FacilityReportRow? report, DateTime utcNow) =>
        report is null
        || (IsTerminal(report.Status) && (report.SubmittedUtc is not DateTime done || utcNow - Utc(done) > ValidationGrace));

    public async Task<ReportProgress> LoadAsync(ReportScheduleApiModel schedule, DateTime utcNow, CancellationToken cancellationToken)
    {
        var reportId = schedule.Id.ToString();
        var terminal = IsTerminal(schedule.Status);

        var entriesTask = Guard("Report", () => _reports?.GetEntrySummaryByScheduleAsync(reportId, cancellationToken));
        var summaryTask = Guard("Report", () => _reports?.GetReportSummaryAsync(reportId, cancellationToken));
        var acquisitionTask = Guard("Data Acquisition", () => _acquisition?.GetReportSummaryAsync(reportId, cancellationToken));
        var normalizationTask = Guard("Normalization", () => _normalization?.GetReportSummaryAsync(reportId, cancellationToken));
        var populationsTask = PopulationsAsync(reportId, terminal, cancellationToken);
        await Task.WhenAll(entriesTask, summaryTask, acquisitionTask, normalizationTask, populationsTask);

        return Build(schedule, entriesTask.Result, summaryTask.Result, acquisitionTask.Result, normalizationTask.Result, populationsTask.Result, utcNow);
    }

    /// <summary>Pure composition, so the stage rules are testable without services.</summary>
    public static ReportProgress Build(
        ReportScheduleApiModel schedule,
        ReportEntrySummaryApiModel? entries,
        ReportSummaryApiModel? summary,
        DataAcquisitionReportSummaryApiModel? acquisition,
        NormalizationReportSummaryApiModel? normalization,
        IReadOnlyDictionary<string, int>? initialPopulation,
        DateTime utcNow)
    {
        var terminal = IsTerminal(schedule.Status);
        var progress = new ReportProgress { Terminal = terminal };

        // Timing: time to submit runs from whichever is later, the period end or the report's creation.
        var periodEnd = Utc(schedule.ReportEndDate);
        var created = schedule.CreateDate is DateTime c ? Utc(c) : (DateTime?)null;
        var submitted = schedule.SubmitReportDateTime is DateTime s ? Utc(s) : (DateTime?)null;
        progress.PeriodEndUtc = periodEnd;
        progress.CreatedUtc = created;
        progress.SubmittedUtc = submitted;
        var from = created is DateTime made && made > periodEnd ? made : periodEnd;
        progress.TimeToSubmitFromUtc = from;
        if (submitted is DateTime done)
            progress.TimeToSubmit = done >= from ? done - from : TimeSpan.Zero;
        else if (!terminal && utcNow >= from)
        {
            progress.TimeToSubmit = utcNow - from;
            progress.TimeToSubmitRunning = true;
        }

        // Entry-level counts.
        var reporting = entries?.ReportingStatusCounts ?? new Dictionary<string, int>();
        var submissions = entries?.SubmissionStatusCounts ?? new Dictionary<string, int>();
        int R(string key) => Get(reporting, key);
        int S(string key) => Get(submissions, key);
        var identified = R(nameof(ReportingStatus.PatientIdentified));
        var notReportable = R(nameof(ReportingStatus.NotReportable));
        var pendingValidation = R(nameof(ReportingStatus.PendingValidation));
        var passed = R(nameof(ReportingStatus.PassedValidation));
        var failed = R(nameof(ReportingStatus.FailedValidation));
        var entryTotal = reporting.Values.Sum();

        // Acquisition.
        var acquisitionRow = new PipelineRow { Key = "acquisition", Name = "Data acquisition" };
        if (acquisition is null)
            acquisitionRow.State = PipelineState.Unavailable;
        else
        {
            int A(string key) => acquisition.StatusCounts.Where(x => string.Equals(x.Status, key, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Count);
            var open = A("Pending") + A("Ready") + A("Queued") + A("Processing");
            var failedLogs = A("Failed");
            acquisitionRow.Counts.Add(new PipelineCount("Patients", acquisition.TotalPatients));
            acquisitionRow.Counts.Add(new PipelineCount("Logs", acquisition.TotalLogs));
            if (open > 0) acquisitionRow.Counts.Add(new PipelineCount("Open", open, "au-badge-warning"));
            if (failedLogs > 0) acquisitionRow.Counts.Add(new PipelineCount("Failed", failedLogs, "au-badge-danger"));
            acquisitionRow.Counts.Add(new PipelineCount("Resources", acquisition.TotalResourcesAcquired));
            acquisitionRow.State = open > 0 ? PipelineState.Active
                : acquisition.TotalLogs > 0 ? PipelineState.Done
                : terminal ? PipelineState.Done : PipelineState.Waiting;
            acquisitionRow.Pending = open;
        }

        // Normalization.
        var normalizationRow = new PipelineRow { Key = "normalization", Name = "Normalization" };
        if (normalization is null)
            normalizationRow.State = PipelineState.Unavailable;
        else
        {
            normalizationRow.Counts.Add(new PipelineCount("Resources", normalization.ResourcesNormalized));
            normalizationRow.Counts.Add(new PipelineCount("Batches", normalization.Passes));
            if (normalization.Failures > 0) normalizationRow.Counts.Add(new PipelineCount("Failed steps", normalization.Failures, "au-badge-danger"));
            normalizationRow.LastActivityUtc = normalization.LastNormalizedUtc is DateTime n ? Utc(n) : null;
            var recent = normalizationRow.LastActivityUtc is DateTime last && utcNow - last < RecentActivity;
            normalizationRow.State = normalization.Passes == 0
                ? (terminal ? PipelineState.Done : PipelineState.Waiting)
                : (!terminal && (acquisitionRow.State == PipelineState.Active || recent)) ? PipelineState.Active : PipelineState.Done;
        }

        // Evaluation.
        var evaluationRow = new PipelineRow { Key = "evaluation", Name = "Measure evaluation" };
        if (entries is null)
            evaluationRow.State = PipelineState.Unavailable;
        else
        {
            var evaluated = entryTotal - identified;
            evaluationRow.Counts.Add(new PipelineCount("Evaluated", evaluated));
            if (identified > 0) evaluationRow.Counts.Add(new PipelineCount("Waiting", identified, "au-badge-warning"));
            if (notReportable > 0) evaluationRow.Counts.Add(new PipelineCount("Not reportable", notReportable));
            evaluationRow.Pending = identified;
            evaluationRow.State = identified > 0 ? PipelineState.Active
                : evaluated > 0 || terminal ? PipelineState.Done : PipelineState.Waiting;
        }

        // Validation.
        var validationRow = new PipelineRow { Key = "validation", Name = "Validation" };
        if (entries is null)
            validationRow.State = PipelineState.Unavailable;
        else
        {
            validationRow.Counts.Add(new PipelineCount("Passed", passed, passed > 0 ? "au-badge-success" : null));
            if (failed > 0) validationRow.Counts.Add(new PipelineCount("Failed", failed, "au-badge-danger"));
            if (pendingValidation > 0) validationRow.Counts.Add(new PipelineCount("Waiting", pendingValidation, "au-badge-warning"));
            validationRow.Pending = pendingValidation;
            validationRow.State = pendingValidation > 0 ? PipelineState.Active
                : passed + failed > 0 || terminal ? PipelineState.Done : PipelineState.Waiting;
        }

        // Submission.
        var submissionRow = new PipelineRow { Key = "submission", Name = "Submission" };
        var submittedCount = S(nameof(SubmissionStatus.Submitted));
        var submitting = S(nameof(SubmissionStatus.Submitting)) + S(nameof(SubmissionStatus.PendingValidation));
        var failedSubmission = S(nameof(SubmissionStatus.FailedSubmission));
        if (entries is null)
            submissionRow.State = PipelineState.Unavailable;
        else
        {
            submissionRow.Counts.Add(new PipelineCount("Submitted", submittedCount, submittedCount > 0 ? "au-badge-success" : null));
            if (submitting > 0) submissionRow.Counts.Add(new PipelineCount("Waiting", submitting, "au-badge-warning"));
            if (failedSubmission > 0) submissionRow.Counts.Add(new PipelineCount("Failed", failedSubmission, "au-badge-danger"));
            submissionRow.Pending = submitting;
            submissionRow.LastActivityUtc = submitted;
            submissionRow.State = terminal ? PipelineState.Done
                : submitting > 0 ? PipelineState.Active : PipelineState.Waiting;
        }

        progress.Pipeline.AddRange([acquisitionRow, normalizationRow, evaluationRow, validationRow, submissionRow]);

        // Current stage: the first stage still working; otherwise where the report is waiting.
        var active = progress.Pipeline.FirstOrDefault(row => row.State == PipelineState.Active);
        progress.Stage = schedule.Status switch
        {
            ScheduleStatus.Submitted => "Submitted",
            ScheduleStatus.CompletedNotSubmitted => "Completed, not submitted",
            _ when active is not null => active.Name,
            ScheduleStatus.New or ScheduleStatus.Scheduled => "Collecting census",
            _ => "Finishing the report"
        };
        progress.StageBadge = schedule.Status switch
        {
            ScheduleStatus.Submitted => "au-badge-success",
            ScheduleStatus.CompletedNotSubmitted => "au-badge-danger",
            _ => "au-badge-active"
        };

        var lastSeen = new List<DateTime?>
        {
            summary?.LastUpdatedDate is DateTime u ? Utc(u) : null,
            normalizationRow.LastActivityUtc,
            submitted
        };
        progress.LastActivityUtc = lastSeen.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty().Max() is var max && max == default ? null : max;

        // Submission breakdown: green done, yellow waiting, red failed or held back.
        void Slice(string name, int count, string color) { if (count > 0) progress.Submission.Add(new ProgressSlice(name, count, color)); }
        if (entries is not null)
        {
            Slice("Submitted", submittedCount, "var(--au-success)");
            Slice("Waiting", submitting, "var(--au-warning)");
            Slice("Failed", failedSubmission, "var(--au-danger)");
            Slice("Not submitted", S(nameof(SubmissionStatus.NotSubmitted)), "var(--au-danger)");
            Slice("Not eligible", S(nameof(SubmissionStatus.NotEligable)), "var(--au-danger)");
            var counted = progress.Submission.Sum(x => x.Count);
            Slice("No status yet", Math.Max(0, entryTotal - counted), "var(--au-warning)");
        }
        progress.SubmissionAvailable = entries is not null;

        // Patients per measure: evaluated from entries, IP from the same populations the Populations tab reads.
        var types = (entries?.ReportTypeCounts ?? new Dictionary<string, int>()).Keys
            .Concat(schedule.ReportTypes ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var type in types)
        {
            int? patients = entries is null ? null : Get(entries.ReportTypeCounts, type);
            // Entry status is per patient, not per measure: exact once nothing waits, or when there is one measure.
            int? evaluated = patients is not int count ? null
                : identified == 0 ? count
                : types.Count == 1 ? Math.Max(0, count - identified)
                : null;
            progress.Measures.Add(new MeasureProgress
            {
                Measure = type,
                Patients = patients,
                Evaluated = evaluated,
                InitialPopulation = initialPopulation is null ? null : Get(initialPopulation, type)
            });
        }
        progress.MeasuresAvailable = entries is not null;
        progress.PopulationAvailable = initialPopulation is not null;
        progress.Unavailable = progress.Pipeline.Where(r => r.State == PipelineState.Unavailable).Select(r => r.Name).ToList();
        return progress;
    }

    private async Task<IReadOnlyDictionary<string, int>?> PopulationsAsync(string reportId, bool terminal, CancellationToken cancellationToken)
    {
        if (_reports is null)
            return null;
        var key = "report-progress-ip:" + reportId;
        if (_cache.TryGetValue(key, out IReadOnlyDictionary<string, int>? cached) && cached is not null)
            return cached;
        var response = await Guard("Report", () => _reports.GetPopulationsByScheduleAsync(reportId, cancellationToken: cancellationToken));
        if (response is null)
            return null;
        var map = InitialPopulationByType(response);
        _cache.Set(key, map, terminal ? PopulationCacheFinal : PopulationCacheLive);
        return map;
    }

    public static IReadOnlyDictionary<string, int> InitialPopulationByType(IEnumerable<ReportPopulationApiModel> populations)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var population in populations)
        {
            if (string.IsNullOrWhiteSpace(population.ReportType))
                continue;
            var ip = (population.GroupPopulations ?? [])
                .Where(g => string.Equals(g.PopulationId, "initial-population", StringComparison.OrdinalIgnoreCase))
                .Sum(g => g.TotalPopulationCount);
            map[population.ReportType] = map.GetValueOrDefault(population.ReportType) + ip;
        }
        return map;
    }

    private async Task<T?> Guard<T>(string service, Func<Task<LinkApiResponse<T>>?> call) where T : class
    {
        try
        {
            var task = call();
            if (task is null)
                return null;
            var response = await task;
            return response.IsSuccessStatusCode ? response.Body : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Report progress could not read the {Service} service.", service);
            return null;
        }
    }

    private static int Get(IReadOnlyDictionary<string, int> map, string key) =>
        map.TryGetValue(key, out var value) ? value
        : map.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    private static int Get(Dictionary<string, int> map, string key) => Get((IReadOnlyDictionary<string, int>)map, key);

    private static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}