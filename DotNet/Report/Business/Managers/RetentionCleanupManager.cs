using System.Text.Json;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
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

        var included = request.Reports
            .Where(r => includedIds.Contains(r.ReportScheduleId))
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
}
