using System.Diagnostics;
using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using LantanaGroup.Link.Shared.Application.Services.Security;

namespace LantanaGroup.Link.Report.Services;

/// <summary>
/// The scheduled retention check: re-sends unanswered requests, then records and requests a cleanup for every
/// report past the retention window, in batches. See dev-docs/retention-check.md.
/// </summary>
/// <remarks>
/// <para>
/// The cutoff is computed once per run, and every cleanup the run creates shares it and the run id.
/// </para>
/// <para>
/// A replayed or recovered run is safe: the cleanup manager claims only reports still <c>None</c>, so a report
/// already put in a cleanup is never selected again. A cleanup is recorded before it is published and marked
/// sent only after the broker accepts it, so a crash in between leaves a cleanup that was never sent, not a
/// request with no record.
/// </para>
/// </remarks>
public class RetentionCheck : IRetentionCheckWorker
{
    /// <summary>
    /// The initiator recorded on a scheduled cleanup, the convention audit events already use.
    /// </summary>
    public const string SystemUser = "SystemUser";

    private readonly RetentionCheckSettings _settings;
    private readonly IRetentionPendingResender _resender;
    private readonly IRetentionEligibilityQuery _eligibilityQuery;
    private readonly IRetentionCleanupManager _cleanupManager;
    private readonly IRetentionCheckRequestedPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RetentionCheck> _logger;

    /// <summary>
    /// Creates the retention check.
    /// </summary>
    public RetentionCheck(RetentionCheckSettings settings,
                          IRetentionPendingResender resender,
                          IRetentionEligibilityQuery eligibilityQuery,
                          IRetentionCleanupManager cleanupManager,
                          IRetentionCheckRequestedPublisher publisher,
                          TimeProvider timeProvider,
                          ILogger<RetentionCheck> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _resender = resender ?? throw new ArgumentNullException(nameof(resender));
        _eligibilityQuery = eligibilityQuery ?? throw new ArgumentNullException(nameof(eligibilityQuery));
        _cleanupManager = cleanupManager ?? throw new ArgumentNullException(nameof(cleanupManager));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs one scheduled retention check. Does nothing but log an error when the retention settings are
    /// invalid.
    /// </summary>
    public async Task RunAsync(RetentionCheckRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!_settings.IsValid)
        {
            _logger.LogError(
                "No retention check ran for the firing due at {ScheduledFireTimeUtc:O}, because the retention " +
                "check settings are invalid: {Errors}",
                run.ScheduledFireTimeUtc,
                string.Join(" ", _settings.Errors).SanitizeForLog());

            return;
        }

        using var activity = ServiceActivitySource.Instance?
            .StartActivity($"{nameof(RetentionCheck)}.Run");

        var stopwatch = Stopwatch.StartNew();
        var runId = RetentionRunIds.For(run.ScheduledFireTimeUtc);
        activity?.SetTag("retention.run_id", runId);

        // Re-sends first, so a request that went unanswered is not left waiting behind a large new backlog.
        await _resender.ResendAsync(run, cancellationToken);

        var window = _settings.Window!;
        var cutoff = RetentionCutoff.From(_timeProvider.GetUtcNow(), window);
        activity?.SetTag("retention.cutoff", cutoff.ToString("O"));

        var (cleanups, reports, skipped) = await RequestCleanupsAsync(runId, window, cutoff, cancellationToken);

        activity?.SetTag("retention.cleanups", cleanups);
        activity?.SetTag("retention.reports", reports);

        _logger.LogInformation(
            "Retention check for run {RunId} finished in {ElapsedMilliseconds} ms: cutoff {Cutoff:O} from window " +
            "{WindowText}; {CleanupCount} cleanups requested for {ReportCount} reports; {SkippedCount} reports " +
            "skipped because another cleanup already took them.",
            runId,
            stopwatch.ElapsedMilliseconds,
            cutoff,
            _settings.WindowText.SanitizeForLog(),
            cleanups,
            reports,
            skipped);
    }

    private async Task<(int Cleanups, int Reports, int Skipped)> RequestCleanupsAsync(
        Guid runId,
        RetentionWindow window,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        var cleanups = 0;
        var reports = 0;
        var skipped = 0;
        var seen = new HashSet<Guid>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = await _eligibilityQuery.FindAsync(cutoff, _settings.MaxReportsPerCleanup, cancellationToken);
            if (batch.Count == 0)
            {
                return (cleanups, reports, skipped);
            }

            // Every report in a batch leaves None or is recorded failed, which ends the run, so seeing one again
            // means the loop would never end.
            if (batch.Any(r => !seen.Add(r.ReportScheduleId)))
            {
                _logger.LogError(
                    "Retention check for run {RunId} stopped: a report it had already handled was selected again, " +
                    "so its retention status did not change.",
                    runId);

                return (cleanups, reports, skipped);
            }

            var creation = await _cleanupManager.CreateAsync(BuildRequest(runId, window, cutoff, batch),
                                                             cancellationToken);
            skipped += creation.SkippedReportScheduleIds.Count;

            if (creation.Request is null)
            {
                if (creation.Status == RetentionCleanupStatus.Failed)
                {
                    // Every further batch would be recorded failed the same way, and its reports stay None.
                    _logger.LogWarning(
                        "Retention check for run {RunId} stopped after cleanup {RetentionCleanupId} was recorded " +
                        "Failed without a request: {FailureReason}",
                        runId,
                        creation.RetentionCleanupId,
                        creation.FailureReason.SanitizeForLog());

                    return (cleanups, reports, skipped);
                }

                continue;
            }

            await PublishAsync(creation, cancellationToken);

            cleanups++;
            reports += creation.IncludedReportScheduleIds.Count;
        }
    }

    private async Task PublishAsync(RetentionCleanupCreation creation, CancellationToken cancellationToken)
    {
        var retentionCleanupId = creation.RetentionCleanupId!.Value;

        try
        {
            await _publisher.PublishAsync(creation.Request!, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Retention cleanup {RetentionCleanupId} was recorded but its request could not be published; it " +
                "stays unsent until it is re-sent.",
                retentionCleanupId);

            throw;
        }

        await _cleanupManager.RecordSentAsync(retentionCleanupId, _timeProvider.GetUtcNow(), cancellationToken);
    }

    private static RetentionCleanupRequest BuildRequest(Guid runId,
                                                        RetentionWindow window,
                                                        DateTimeOffset cutoff,
                                                        IReadOnlyList<RetentionEligibleReport> batch)
    {
        return new RetentionCleanupRequest
        {
            RunId = runId,
            InitiatedBy = RetentionInitiatedBy.Scheduled,
            InitiatedByUserId = SystemUser,
            InitiatedByUserName = SystemUser,
            Mode = RetentionMode.Scrub,
            CutoffDateTime = cutoff,
            Reports = batch
                .Select(r => new RetentionCleanupRequestReport
                {
                    ReportScheduleId = r.ReportScheduleId,
                    FacilityId = r.FacilityId,
                    RetentionWindow = new RetentionWindow
                    {
                        Unit = window.Unit,
                        Count = window.Count
                    },
                    CutoffDateTime = cutoff
                })
                .ToList()
        };
    }
}
