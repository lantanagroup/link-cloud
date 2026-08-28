using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Report.Domain.Managers;

public interface IReportEntryMappingOutcomeManager
{
    /// <summary>
    /// Records the DataAcquisition side of a patient's mapping outcome, inserting the row if this is the
    /// first source to report for the pair.
    /// </summary>
    Task UpsertAcquisitionOutcomeAsync(
        string facilityId,
        Guid reportScheduleId,
        string patientId,
        MappingIndicatorStatus locationOrgStatus,
        MappingIndicatorStatus encounterMappingStatus,
        string? acquisitionDetails,
        DateTime evaluatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the Normalization side of a patient's mapping outcome, inserting the row if this is the
    /// first source to report for the pair.
    /// </summary>
    Task UpsertNormalizationOutcomeAsync(
        string facilityId,
        Guid reportScheduleId,
        string patientId,
        MappingIndicatorStatus hslocMappingStatus,
        string? normalizationDetails,
        DateTime evaluatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies every mapping outcome from one report schedule onto another, for regeneration.
    /// </summary>
    /// <returns>The number of rows copied.</returns>
    Task<int> CopyToScheduleAsync(
        Guid sourceReportScheduleId,
        Guid targetReportScheduleId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists per-patient mapping outcomes, one column group per producer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Column ownership is enforced by the API, not by convention.</b> There is a method per source and each
/// writes only its own columns, so the two producers cannot clobber each other whatever order they arrive
/// in. A single method switching on the source would leave that guarantee to a runtime branch.
/// </para>
/// <para>
/// The updates use <c>ExecuteUpdateAsync</c>, which compiles to one UPDATE naming only the listed columns.
/// It loads no entity and takes no snapshot, so two concurrent calls touching disjoint columns serialize on
/// the row lock and neither reverts the other — which is why no concurrency token is needed. Two
/// consequences follow: <c>ModifyDate</c> must be set explicitly because the SaveChanges interceptor never
/// runs, and any tracked copy of the row in the same context is stale afterwards.
/// </para>
/// <para>
/// The update and the insert are separate statements and are not atomic together, so the unique index on
/// (ReportScheduleId, PatientId) plus a caught violation is what makes the insert safe rather than a
/// check-then-insert race.
/// </para>
/// </remarks>
public class ReportEntryMappingOutcomeManager : IReportEntryMappingOutcomeManager
{
    private readonly ReportDbContext _dbContext;
    private readonly ILogger<ReportEntryMappingOutcomeManager> _logger;

    public ReportEntryMappingOutcomeManager(
        ReportDbContext dbContext,
        ILogger<ReportEntryMappingOutcomeManager> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task UpsertAcquisitionOutcomeAsync(
        string facilityId,
        Guid reportScheduleId,
        string patientId,
        MappingIndicatorStatus locationOrgStatus,
        MappingIndicatorStatus encounterMappingStatus,
        string? acquisitionDetails,
        DateTime evaluatedAt,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            reportScheduleId,
            patientId,
            update: rows => rows.ExecuteUpdateAsync(setters => setters
                .SetProperty(outcome => outcome.LocationOrgStatus, locationOrgStatus)
                .SetProperty(outcome => outcome.EncounterMappingStatus, encounterMappingStatus)
                .SetProperty(outcome => outcome.AcquisitionDetails, acquisitionDetails)
                .SetProperty(outcome => outcome.AcquisitionEvaluatedAt, evaluatedAt)
                .SetProperty(outcome => outcome.ModifyDate, evaluatedAt), cancellationToken),
            newRow: () => new ReportEntryMappingOutcome
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ReportScheduleId = reportScheduleId,
                PatientId = patientId,
                LocationOrgStatus = locationOrgStatus,
                EncounterMappingStatus = encounterMappingStatus,
                AcquisitionDetails = acquisitionDetails,
                AcquisitionEvaluatedAt = evaluatedAt,
                CreateDate = evaluatedAt,
                ModifyDate = evaluatedAt
            },
            cancellationToken);

    public Task UpsertNormalizationOutcomeAsync(
        string facilityId,
        Guid reportScheduleId,
        string patientId,
        MappingIndicatorStatus hslocMappingStatus,
        string? normalizationDetails,
        DateTime evaluatedAt,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            reportScheduleId,
            patientId,
            update: rows => rows.ExecuteUpdateAsync(setters => setters
                .SetProperty(outcome => outcome.HslocMappingStatus, hslocMappingStatus)
                .SetProperty(outcome => outcome.NormalizationDetails, normalizationDetails)
                .SetProperty(outcome => outcome.NormalizationEvaluatedAt, evaluatedAt)
                .SetProperty(outcome => outcome.ModifyDate, evaluatedAt), cancellationToken),
            newRow: () => new ReportEntryMappingOutcome
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                ReportScheduleId = reportScheduleId,
                PatientId = patientId,
                HslocMappingStatus = hslocMappingStatus,
                NormalizationDetails = normalizationDetails,
                NormalizationEvaluatedAt = evaluatedAt,
                CreateDate = evaluatedAt,
                ModifyDate = evaluatedAt
            },
            cancellationToken);

    /// <remarks>
    /// <para>
    /// Regeneration re-evaluates the resources the original run already stored, so both mapping steps are
    /// bypassed and neither producer fires. Without this the regenerated report would show every indicator
    /// as never evaluated, permanently.
    /// </para>
    /// <para>
    /// The copied values are that report's true values rather than inherited approximations: the org-location
    /// strip and the code map both ran before those resources were written, so they describe exactly the
    /// resource set the regenerated report evaluates.
    /// </para>
    /// <para>
    /// Both evaluated-at timestamps are carried over verbatim rather than restamped. They honestly record
    /// when the mapping was evaluated, and a timestamp predating the new schedule is itself the signal that
    /// the values came from the original acquisition. That also lets a regenerate-of-a-regenerate chain
    /// without each hop claiming to be fresher than it is.
    /// </para>
    /// </remarks>
    public async Task<int> CopyToScheduleAsync(
        Guid sourceReportScheduleId,
        Guid targetReportScheduleId,
        CancellationToken cancellationToken = default)
    {
        var source = await _dbContext.ReportEntryMappingOutcome
            .AsNoTracking()
            .Where(outcome => outcome.ReportScheduleId == sourceReportScheduleId)
            .ToListAsync(cancellationToken);

        if (source.Count == 0)
        {
            // A report predating this feature has nothing to carry forward, which correctly leaves the new
            // rows absent rather than inventing an outcome for them.
            return 0;
        }

        var now = DateTime.UtcNow;

        _dbContext.ReportEntryMappingOutcome.AddRange(source.Select(outcome => new ReportEntryMappingOutcome
        {
            Id = Guid.NewGuid(),
            FacilityId = outcome.FacilityId,
            ReportScheduleId = targetReportScheduleId,
            PatientId = outcome.PatientId,
            LocationOrgStatus = outcome.LocationOrgStatus,
            EncounterMappingStatus = outcome.EncounterMappingStatus,
            AcquisitionDetails = outcome.AcquisitionDetails,
            AcquisitionEvaluatedAt = outcome.AcquisitionEvaluatedAt,
            HslocMappingStatus = outcome.HslocMappingStatus,
            NormalizationDetails = outcome.NormalizationDetails,
            NormalizationEvaluatedAt = outcome.NormalizationEvaluatedAt,

            // CreateDate describes this row, not the evaluation it carries.
            CreateDate = now
        }));

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Copied {Count} mapping outcome(s) from report schedule {SourceReportScheduleId} to {TargetReportScheduleId}.",
            source.Count, sourceReportScheduleId, targetReportScheduleId);

        return source.Count;
    }

    private async Task UpsertAsync(
        Guid reportScheduleId,
        string patientId,
        Func<IQueryable<ReportEntryMappingOutcome>, Task<int>> update,
        Func<ReportEntryMappingOutcome> newRow,
        CancellationToken cancellationToken)
    {
        var rows = _dbContext.ReportEntryMappingOutcome
            .Where(outcome => outcome.ReportScheduleId == reportScheduleId && outcome.PatientId == patientId);

        if (await update(rows) > 0)
        {
            return;
        }

        try
        {
            _dbContext.ReportEntryMappingOutcome.Add(newRow());
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            // The other source inserted the row between the update and the insert. Retry as an update; it
            // touches only this source's columns, so the row the other source just wrote is preserved.
            _logger.LogDebug(
                "Mapping outcome row for schedule {ReportScheduleId} was inserted concurrently; retrying as an update.",
                reportScheduleId);

            // The failed Add is still tracked as Added; leaving it there would replay on the next
            // SaveChanges in this scope.
            _dbContext.ChangeTracker.Clear();

            await update(rows);
        }
    }

    /// <remarks>
    /// Matched on the message rather than on <see cref="SqlException"/> alone, so the retry path behaves
    /// the same against SQLite as it does against SQL Server. A provider-specific check would leave this
    /// branch silently dead everywhere except production, which is the one place it must not be wrong.
    /// </remarks>
    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        // SQL Server error 2601 = unique index violation, 2627 = unique constraint violation.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 })
            {
                return true;
            }
        }

        var message = exception.InnerException?.Message ?? exception.Message;

        return message.Contains("2601")
            || message.Contains("2627")
            || message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }
}
