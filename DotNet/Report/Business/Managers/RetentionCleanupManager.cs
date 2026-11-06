using System.Text.Json;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using LantanaGroup.Link.Shared.Application.Services.Security;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Report.Domain.Managers;

/// <summary>
/// Records retention cleanups. See dev-docs/retention-cleanup-record.md.
/// </summary>
public interface IRetentionCleanupManager
{
    /// <summary>
    /// Records a cleanup for the requested reports that are still <c>None</c>, in one transaction, and returns
    /// the request to publish. With an invalid participant list the cleanup is recorded failed, the reports
    /// stay <c>None</c>, and there is nothing to publish.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The request breaks a contract rule, such as an empty report list.
    /// </exception>
    Task<RetentionCleanupCreation> CreateAsync(RetentionCleanupRequest request,
                                               CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the cleanup's request was sent to every participant at <paramref name="sentDateTime"/>.
    /// Call it after the publish succeeds, so a crash in between leaves the results unsent.
    /// </summary>
    /// <returns>The number of results updated.</returns>
    Task<int> RecordSentAsync(Guid retentionCleanupId,
                              DateTimeOffset sentDateTime,
                              CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies one participant's completion: its results, the cleanup's recalculated status, and
    /// <c>Scrubbed</c> for every report all report-scoped participants have completed. Duplicate and stale
    /// replies change nothing.
    /// </summary>
    Task<RetentionCompletionOutcome> ApplyCompletionAsync(Guid retentionCleanupId,
                                                          RetentionCheckCompleteValue completion,
                                                          CancellationToken cancellationToken = default);
}

/// <summary>
/// Records retention cleanups: the cleanup, its reports, the participant snapshot and a pending result per
/// participant and report.
/// </summary>
/// <remarks>
/// <para>
/// A report is taken by one <c>UPDATE ... OUTPUT</c> that moves it from <c>None</c> to <c>PendingDeletion</c>.
/// A concurrent cleanup asking for the same report waits on the row lock and then finds it no longer
/// <c>None</c>, so a report can never be in two cleanups that send requests.
/// </para>
/// <para>
/// Sending is recorded separately, by <see cref="RecordSentAsync"/>, once the publish has succeeded.
/// </para>
/// </remarks>
public class RetentionCleanupManager : IRetentionCleanupManager
{
    private const int MaxFailureReasonLength = 2000;

    private readonly ReportDbContext _context;
    private readonly RetentionParticipants _participants;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RetentionCleanupManager> _logger;

    /// <summary>
    /// Creates the manager.
    /// </summary>
    public RetentionCleanupManager(ReportDbContext context,
                                   RetentionParticipants participants,
                                   TimeProvider timeProvider,
                                   ILogger<RetentionCleanupManager> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _participants = participants ?? throw new ArgumentNullException(nameof(participants));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<RetentionCleanupCreation> CreateAsync(RetentionCleanupRequest request,
                                                            CancellationToken cancellationToken = default)
    {
        Validate(request);

        var now = _timeProvider.GetUtcNow();
        var requestedIds = request.Reports.Select(r => r.ReportScheduleId).ToList();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var includedIds = _participants.IsValid
            ? await ClaimAsync(requestedIds, now, cancellationToken)
            : await FindStillNoneAsync(requestedIds, cancellationToken);
        var skippedIds = requestedIds.Except(includedIds).ToList();

        if (includedIds.Count == 0)
        {
            _logger.LogWarning(
                "No retention cleanup recorded for run {RunId}: none of the {Count} requested reports is still " +
                "None.",
                request.RunId,
                requestedIds.Count);

            return new RetentionCleanupCreation { SkippedReportScheduleIds = skippedIds };
        }

        var includedIdSet = includedIds.ToHashSet();
        var included = request.Reports
            .Where(r => includedIdSet.Contains(r.ReportScheduleId))
            .ToList();
        var cleanup = BuildCleanup(request, included, now);

        _context.RetentionCleanup.Add(cleanup);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        LogCreated(cleanup, request, skippedIds.Count);

        return new RetentionCleanupCreation
        {
            RetentionCleanupId = cleanup.Id,
            Status = cleanup.Status,
            FailureReason = cleanup.FailureReason,
            IncludedReportScheduleIds = included.Select(r => r.ReportScheduleId).ToList(),
            SkippedReportScheduleIds = skippedIds,
            Request = cleanup.Status == RetentionCleanupStatus.InProgress ? BuildMessage(cleanup) : null
        };
    }

    /// <inheritdoc />
    public async Task<int> RecordSentAsync(Guid retentionCleanupId,
                                           DateTimeOffset sentDateTime,
                                           CancellationToken cancellationToken = default)
    {
        return await _context.RetentionCleanupResult
            .Where(r => r.RetentionCleanupParticipant.RetentionCleanupId == retentionCleanupId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.LastSentDateTime, sentDateTime)
                .SetProperty(r => r.SendCount, r => r.SendCount + 1),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RetentionCompletionOutcome> ApplyCompletionAsync(Guid retentionCleanupId,
                                                                       RetentionCheckCompleteValue completion,
                                                                       CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completion);

        if (string.IsNullOrWhiteSpace(completion.ServiceName))
        {
            throw new ArgumentException("A completion needs the participant's service name.", nameof(completion));
        }

        if (!RetentionCleanupRules.HasDefinedStatuses(completion))
        {
            throw new ArgumentException("Every status in a completion must be Completed or Failed.",
                                        nameof(completion));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (!await LockCleanupAsync(retentionCleanupId, cancellationToken))
        {
            _logger.LogWarning(
                "Ignored a retention completion from {ServiceName} for unknown cleanup {RetentionCleanupId}.",
                completion.ServiceName.SanitizeForLog(),
                retentionCleanupId);

            return RetentionCompletionOutcome.UnknownCleanup;
        }

        var cleanup = await _context.RetentionCleanup
            .Include(c => c.Reports)
            .Include(c => c.Participants)
            .ThenInclude(p => p.Results)
            .AsSplitQuery()
            .SingleAsync(c => c.Id == retentionCleanupId, cancellationToken);

        var participant = cleanup.Participants.SingleOrDefault(p =>
            string.Equals(p.ServiceName, completion.ServiceName, StringComparison.OrdinalIgnoreCase));

        if (participant is null)
        {
            _logger.LogWarning(
                "Ignored a retention completion for cleanup {RetentionCleanupId} from {ServiceName}, which is " +
                "not one of its expected participants.",
                retentionCleanupId,
                completion.ServiceName.SanitizeForLog());

            return RetentionCompletionOutcome.UnexpectedService;
        }

        var tally = participant.Scope == RetentionParticipantScope.Cutoff
            ? ApplySummary(participant, completion)
            : ApplyReports(cleanup, participant, completion);

        var now = _timeProvider.GetUtcNow();
        var previousStatus = cleanup.Status;
        RecalculateStatus(cleanup, now);
        var scrubbed = ReportsReadyToScrub(cleanup);

        await _context.SaveChangesAsync(cancellationToken);
        var scrubbedCount = await MarkScrubbedAsync(scrubbed, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Applied the retention completion from {ServiceName} to cleanup {RetentionCleanupId}: {Changed} " +
            "results changed, {Unchanged} unchanged, {Missing} missing and recorded Failed, {Unknown} unknown " +
            "reports ignored. Status {PreviousStatus} -> {Status}; {ScrubbedCount} reports now Scrubbed.",
            participant.ServiceName.SanitizeForLog(),
            cleanup.Id,
            tally.Changed,
            tally.Unchanged,
            tally.Missing,
            tally.Unknown,
            previousStatus,
            cleanup.Status,
            scrubbedCount);

        return RetentionCompletionOutcome.Applied;
    }

    private static void Validate(RetentionCleanupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Reports is null || request.Reports.Count == 0)
        {
            throw new ArgumentException("A retention cleanup needs at least one report.", nameof(request));
        }

        if (request.Reports.Select(r => r.ReportScheduleId).Distinct().Count() != request.Reports.Count)
        {
            throw new ArgumentException("A report can appear only once in a retention cleanup.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.InitiatedByUserId) ||
            string.IsNullOrWhiteSpace(request.InitiatedByUserName))
        {
            throw new ArgumentException("A retention cleanup needs the initiating user's id and name.",
                                        nameof(request));
        }

        if (request.Reports.Any(r => string.IsNullOrWhiteSpace(r.FacilityId)))
        {
            throw new ArgumentException("Every report in a retention cleanup needs its facility.", nameof(request));
        }

        var singleReport = request.InitiatedBy == RetentionInitiatedBy.SingleReport;
        if (request.Mode == RetentionMode.Purge && !singleReport)
        {
            throw new ArgumentException("Only a single-report cleanup can purge.", nameof(request));
        }

        if (singleReport && request.Reports.Count != 1)
        {
            throw new ArgumentException("A single-report cleanup covers exactly one report.", nameof(request));
        }

        // No window selects the report on a single-report cleanup; every other cleanup is window-driven.
        if (request.Reports.Any(r => (r.RetentionWindow is null) != singleReport))
        {
            throw new ArgumentException(
                singleReport
                    ? "A single-report cleanup has no retention window."
                    : "Every report in a scheduled or manual cleanup needs the window that selected it.",
                nameof(request));
        }
    }

    // One statement takes every report that is still None. OPENJSON keeps it to one parameter whatever the
    // batch size. EF runs the SQL as written because nothing is composed on top of it.
    private async Task<List<Guid>> ClaimAsync(List<Guid> reportScheduleIds,
                                              DateTimeOffset now,
                                              CancellationToken cancellationToken)
    {
        var idsJson = JsonSerializer.Serialize(reportScheduleIds);
        var pending = nameof(ReportRetentionStatus.PendingDeletion);
        var none = nameof(ReportRetentionStatus.None);
        var modified = now.UtcDateTime;

        return await _context.Database
            .SqlQuery<Guid>($"""
                UPDATE [ReportSchedule]
                SET [RetentionStatus] = {pending}, [ModifyDate] = {modified}
                OUTPUT INSERTED.[Id] AS [Value]
                WHERE [RetentionStatus] = {none}
                  AND [Id] IN (SELECT CAST([value] AS uniqueidentifier) FROM OPENJSON({idsJson}))
                """)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> FindStillNoneAsync(List<Guid> reportScheduleIds,
                                                      CancellationToken cancellationToken)
    {
        return await _context.ReportSchedule
            .Where(r => reportScheduleIds.Contains(r.Id) && r.RetentionStatus == ReportRetentionStatus.None)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    private RetentionCleanup BuildCleanup(RetentionCleanupRequest request,
                                          List<RetentionCleanupRequestReport> reports,
                                          DateTimeOffset now)
    {
        var cleanup = new RetentionCleanup
        {
            Id = Guid.NewGuid(),
            RunId = request.RunId,
            InitiatedBy = request.InitiatedBy,
            InitiatedDateTime = now,
            InitiatedByUserId = request.InitiatedByUserId,
            InitiatedByUserName = request.InitiatedByUserName,
            Mode = request.Mode,
            CutoffDateTime = request.CutoffDateTime,
            Status = RetentionCleanupStatus.InProgress
        };

        foreach (var report in reports)
        {
            cleanup.Reports.Add(new RetentionCleanupReport
            {
                Id = Guid.NewGuid(),
                ReportScheduleId = report.ReportScheduleId,
                FacilityId = report.FacilityId,
                RetentionWindowUnit = report.RetentionWindow?.Unit,
                RetentionWindowCount = report.RetentionWindow?.Count,
                CutoffDateTime = report.CutoffDateTime
            });
        }

        // A failed cleanup keeps its reports for the history, but expects no one and sends nothing.
        if (!_participants.IsValid)
        {
            cleanup.Status = RetentionCleanupStatus.Failed;
            cleanup.FailureReason = Truncate($"{RetentionParticipants.Key} is invalid: " +
                                             string.Join(" ", _participants.Errors));
            return cleanup;
        }

        foreach (var configured in _participants.Participants)
        {
            var participant = new RetentionCleanupParticipant
            {
                Id = Guid.NewGuid(),
                ServiceName = configured.ServiceName,
                Scope = configured.Scope
            };
            AddPendingResults(participant, cleanup.Reports);
            cleanup.Participants.Add(participant);
        }

        return cleanup;
    }

    private static void AddPendingResults(RetentionCleanupParticipant participant,
                                          ICollection<RetentionCleanupReport> reports)
    {
        if (participant.Scope == RetentionParticipantScope.Cutoff)
        {
            participant.Results.Add(new RetentionCleanupResult
            {
                Id = Guid.NewGuid(),
                Status = RetentionCleanupResultStatus.Pending
            });
            return;
        }

        foreach (var report in reports)
        {
            participant.Results.Add(new RetentionCleanupResult
            {
                Id = Guid.NewGuid(),
                RetentionCleanupReport = report,
                Status = RetentionCleanupResultStatus.Pending
            });
        }
    }

    private static RetentionCheckRequestedValue BuildMessage(RetentionCleanup cleanup)
    {
        return new RetentionCheckRequestedValue
        {
            RetentionCleanupId = cleanup.Id.ToString(),
            InitiatedBy = cleanup.InitiatedBy,
            InitiatedDateTime = cleanup.InitiatedDateTime,
            Mode = cleanup.Mode,
            CutoffDateTime = cleanup.CutoffDateTime,
            Reports = cleanup.Reports
                .Select(r => new RetentionReport
                {
                    FacilityId = r.FacilityId,
                    ReportScheduleId = r.ReportScheduleId.ToString(),
                    RetentionWindow = r.RetentionWindowUnit is { } unit && r.RetentionWindowCount is { } count
                        ? new RetentionWindow { Unit = unit, Count = count }
                        : null,
                    CutoffDateTime = r.CutoffDateTime
                })
                .ToList()
        };
    }

    private void LogCreated(RetentionCleanup cleanup, RetentionCleanupRequest request, int skippedCount)
    {
        if (cleanup.Status == RetentionCleanupStatus.Failed)
        {
            _logger.LogError(
                "Retention cleanup {RetentionCleanupId} for run {RunId} recorded Failed with {ReportCount} reports " +
                "left at None, because {Key} is invalid. No request will be sent.",
                cleanup.Id,
                request.RunId,
                cleanup.Reports.Count,
                RetentionParticipants.Key);
            return;
        }

        _logger.LogInformation(
            "Recorded retention cleanup {RetentionCleanupId} for run {RunId}: {ReportCount} reports, " +
            "{ParticipantCount} participants, {SkippedCount} requested reports skipped.",
            cleanup.Id,
            request.RunId,
            cleanup.Reports.Count,
            cleanup.Participants.Count,
            skippedCount);
    }

    private static string Truncate(string text)
    {
        return text.Length <= MaxFailureReasonLength ? text : text[..MaxFailureReasonLength];
    }

    // Two instances can hold the same completion during a rebalance. The update lock makes the second wait,
    // so each recalculates the status from committed results.
    private async Task<bool> LockCleanupAsync(Guid retentionCleanupId, CancellationToken cancellationToken)
    {
        var locked = await _context.Database
            .SqlQuery<Guid>($"""
                SELECT [Id] AS [Value]
                FROM [RetentionCleanup] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = {retentionCleanupId}
                """)
            .ToListAsync(cancellationToken);

        return locked.Count == 1;
    }

    private static (int Changed, int Unchanged, int Missing, int Unknown) ApplySummary(
        RetentionCleanupParticipant participant,
        RetentionCheckCompleteValue completion)
    {
        var result = participant.Results.Single();

        if (completion.Summary is null)
        {
            var missing = RetentionCleanupRules.ApplyMissing(result,
                                                             participant.ServiceName,
                                                             completion.CompletedDateTime);
            return missing ? (0, 0, 1, 0) : (0, 1, 0, 0);
        }

        return RetentionCleanupRules.ApplyReply(result, completion.Summary) ? (1, 0, 0, 0) : (0, 1, 0, 0);
    }

    private (int Changed, int Unchanged, int Missing, int Unknown) ApplyReports(
        RetentionCleanup cleanup,
        RetentionCleanupParticipant participant,
        RetentionCheckCompleteValue completion)
    {
        var (changed, unchanged, missing, unknown) = (0, 0, 0, 0);
        var replies = new Dictionary<Guid, RetentionReportResult>();
        var reportScheduleIds = cleanup.Reports
            .Select(r => r.ReportScheduleId)
            .ToHashSet();

        foreach (var reply in completion.Reports ?? [])
        {
            if (Guid.TryParse(reply.ReportScheduleId, out var id) &&
                reportScheduleIds.Contains(id) &&
                replies.TryAdd(id, reply))
            {
                continue;
            }

            unknown++;
        }

        foreach (var result in participant.Results)
        {
            var reportScheduleId = result.RetentionCleanupReport.ReportScheduleId;

            if (replies.TryGetValue(reportScheduleId, out var reply))
            {
                if (RetentionCleanupRules.ApplyReply(result, reply))
                {
                    changed++;
                    continue;
                }

                unchanged++;
                continue;
            }

            if (RetentionCleanupRules.ApplyMissing(result, participant.ServiceName, completion.CompletedDateTime))
            {
                missing++;
                continue;
            }

            unchanged++;
        }

        if (unknown > 0)
        {
            _logger.LogWarning(
                "The retention completion from {ServiceName} for cleanup {RetentionCleanupId} listed {Count} " +
                "reports that are not in the cleanup, are not valid ids, or appear more than once; they were " +
                "ignored.",
                participant.ServiceName.SanitizeForLog(),
                cleanup.Id,
                unknown);
        }

        return (changed, unchanged, missing, unknown);
    }

    private static void RecalculateStatus(RetentionCleanup cleanup, DateTimeOffset now)
    {
        var results = cleanup.Participants
            .SelectMany(p => p.Results)
            .Select(r => r.Status)
            .ToList();

        cleanup.Status = RetentionCleanupRules.CalculateStatus(results);

        if (cleanup.Status == RetentionCleanupStatus.Completed && cleanup.CompletedDateTime is null)
        {
            cleanup.CompletedDateTime = now;
        }
    }

    // Cutoff-scoped participants have no per-report result, so they never hold a report back. With no
    // report-scoped participant at all, every report is ready.
    private static List<Guid> ReportsReadyToScrub(RetentionCleanup cleanup)
    {
        var resultsByReport = cleanup.Participants
            .Where(p => p.Scope == RetentionParticipantScope.Report)
            .SelectMany(p => p.Results)
            .Where(r => r.RetentionCleanupReportId.HasValue)
            .ToLookup(r => r.RetentionCleanupReportId!.Value);

        return cleanup.Reports
            .Where(report => resultsByReport[report.Id]
                .All(r => r.Status == RetentionCleanupResultStatus.Completed))
            .Select(report => report.ReportScheduleId)
            .ToList();
    }

    private async Task<int> MarkScrubbedAsync(List<Guid> reportScheduleIds,
                                              DateTimeOffset now,
                                              CancellationToken cancellationToken)
    {
        if (reportScheduleIds.Count == 0)
        {
            return 0;
        }

        var modified = now.UtcDateTime;

        return await _context.ReportSchedule
            .Where(r => reportScheduleIds.Contains(r.Id) && r.RetentionStatus == ReportRetentionStatus.PendingDeletion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.RetentionStatus, ReportRetentionStatus.Scrubbed)
                .SetProperty(r => r.ModifyDate, modified),
                cancellationToken);
    }
}
