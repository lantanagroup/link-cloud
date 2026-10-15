using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Business;
using LantanaGroup.Link.DMRP.Business.Managers;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.DMRP.Models;
using LantanaGroup.Link.DMRP.Models.Exceptions;
using LantanaGroup.Link.Sdk.ApiClient;
using LantanaGroup.Link.Sdk.Clients;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.MockDmrp;
using LantanaGroup.Link.Shared.Application.Models.Tenant;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.DMRP.MockDmrp;

/// <summary>
/// With the Mock DMRP API switched on, writes a facility's selected reports to the mock as enrollment before
/// the DMRP operations derive the facility's schedule from it.
/// </summary>
/// <remarks>
/// The selection is the whole of the enrollment, exactly as with DMRP off: a save that selects nothing
/// leaves the facility reporting nothing. Registered around <see cref="DmrpFacilityOperations"/> only while
/// the mock is on, so with it off the DMRP path is unchanged.
/// <para>
/// The mock is written before Tenant's own save and is not part of its transaction. If Tenant then refuses
/// the save, the mock can be ahead of Link until the next save or nightly sync brings them back in line.
/// </para>
/// <para>
/// See dev-docs/mock-dmrp-write-through.md.
/// </para>
/// </remarks>
public sealed class MockDmrpSyncFacilityOperations : IFacilityOperations
{
    private const int PageSize = 100;

    private readonly ILogger<MockDmrpSyncFacilityOperations> _logger;
    private readonly IFacilityOperations _dmrpOperations;
    private readonly IMockDmrpServiceClient _mock;
    private readonly IEntityRepository<MeasureMapping> _measureMappings;
    private readonly IFacilityReportingPeriodResolver _periodResolver;
    private readonly IFacilityExistence _facilityExistence;
    private readonly IDmrpReportingPlanSync _sync;
    private readonly IFacilityReportingPlanManager _reportingPlans;

    /// <summary>
    /// Creates the write-through around <paramref name="dmrpOperations"/>, the DMRP module's own operations.
    /// </summary>
    public MockDmrpSyncFacilityOperations(ILogger<MockDmrpSyncFacilityOperations> logger,
                                          IFacilityOperations dmrpOperations,
                                          IMockDmrpServiceClient mock,
                                          IEntityRepository<MeasureMapping> measureMappings,
                                          IFacilityReportingPeriodResolver periodResolver,
                                          IFacilityExistence facilityExistence,
                                          IDmrpReportingPlanSync sync,
                                          IFacilityReportingPlanManager reportingPlans)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dmrpOperations = dmrpOperations ?? throw new ArgumentNullException(nameof(dmrpOperations));
        _mock = mock ?? throw new ArgumentNullException(nameof(mock));
        _measureMappings = measureMappings ?? throw new ArgumentNullException(nameof(measureMappings));
        _periodResolver = periodResolver ?? throw new ArgumentNullException(nameof(periodResolver));
        _facilityExistence = facilityExistence ?? throw new ArgumentNullException(nameof(facilityExistence));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _reportingPlans = reportingPlans ?? throw new ArgumentNullException(nameof(reportingPlans));
    }

    /// <inheritdoc />
    /// <remarks>
    /// A facility that already exists is left to the host's duplicate check, untouched in the mock: writing
    /// first would hand a live facility the enrollment of a create that is about to be refused. No sync here,
    /// because <see cref="DmrpFacilityOperations.CreateAsync"/> runs one itself.
    /// </remarks>
    public async Task CreateAsync(FacilityModel facility, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facility);

        var facilityId = facility.FacilityId;

        if (!string.IsNullOrWhiteSpace(facilityId) &&
            !await _facilityExistence.ExistsAsync(facilityId, cancellationToken))
        {
            var enrollment = await ResolveEnrollmentAsync(facility.ScheduledReports, cancellationToken);
            var periods = Periods(facilityId, facility.TimeZone);

            var written = await WriteMockAsync(facilityId, enrollment, periods, cancellationToken);

            LogSave("create", facilityId, written, withdrawn: 0);
        }

        facility.ScheduledReports = ReportingPlanScheduleProjector.EmptySchedule();

        await _dmrpOperations.CreateAsync(facility, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The DMRP operations build the schedule from stored plans without asking DMRP, so this syncs the
    /// current period after writing the mock. It then withdraws what the sync cannot: plans for measures
    /// the selection dropped, when DMRP answers with nothing at all or only from another component.
    /// </remarks>
    public async Task UpdateAsync(FacilityModel existingFacility, FacilityModel updatedFacility,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(existingFacility);
        ArgumentNullException.ThrowIfNull(updatedFacility);

        var facilityId = updatedFacility.FacilityId;

        if (!string.IsNullOrWhiteSpace(facilityId))
        {
            var enrollment = await ResolveEnrollmentAsync(updatedFacility.ScheduledReports, cancellationToken);

            // The timezone being saved, as DmrpFacilityOperations reads it, so both use the same period.
            var periods = Periods(facilityId, updatedFacility.TimeZone);

            var written = await WriteMockAsync(facilityId, enrollment, periods, cancellationToken);

            await _sync.SyncAsync(facilityId, periods[0].Month, periods[0].Year, cancellationToken);

            var withdrawn = await _reportingPlans.WithdrawUnselectedAsync(facilityId, periods,
                enrollment.Selected, cancellationToken);

            LogSave("update", facilityId, written, withdrawn);
        }

        updatedFacility.ScheduledReports = ReportingPlanScheduleProjector.EmptySchedule();

        await _dmrpOperations.UpdateAsync(existingFacility, updatedFacility, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The facility is already gone when the mock is cleared, so a failure there is logged rather than
    /// turned into an error for a delete that succeeded. Entries left behind are harmless: nothing reads
    /// the mock for a facility Link does not have.
    /// </remarks>
    public async Task DeleteAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        await _dmrpOperations.DeleteAsync(facilityId, cancellationToken);

        if (string.IsNullOrWhiteSpace(facilityId))
        {
            return;
        }

        var deleted = await _mock.DeleteEntriesForFacilityAsync(facilityId, cancellationToken);

        if (!deleted.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Facility {FacilityId} was deleted, but its Mock DMRP entries could not be removed " +
                "(HTTP {StatusCode}).",
                facilityId.SanitizeForLog(), deleted.StatusCode);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Leaves the mock alone, the same way the DMRP operations keep a soft-deleted facility's reporting plans.
    /// </remarks>
    public Task SoftDeleteAsync(string facilityId, CancellationToken cancellationToken = default) =>
        _dmrpOperations.SoftDeleteAsync(facilityId, cancellationToken);

    /// <inheritdoc />
    public Task RestoreAsync(FacilityModel facility, CancellationToken cancellationToken = default) =>
        _dmrpOperations.RestoreAsync(facility, cancellationToken);

    /// <summary>
    /// The NHSN measures the selection enrolls, and the measures the write-through manages.
    /// </summary>
    /// <param name="Selected">
    /// Every measure mapped to a selected dQM at the selected frequency. Several can share one dQM.
    /// </param>
    /// <param name="Managed">
    /// Every measure mapped to a dQM. Only these are added to or removed from the mock: a measure with no dQM
    /// cannot appear on the form, so the form has no say over it.
    /// </param>
    private sealed record Enrollment(IReadOnlySet<string> Selected, IReadOnlySet<string> Managed);

    /// <summary>
    /// Turns the selected (dQM, frequency) pairs into NHSN measures through the measure mappings.
    /// </summary>
    /// <exception cref="UnmappedDqmSelectionException">
    /// A selected pair has no mapping. Thrown before anything is written.
    /// </exception>
    private async Task<Enrollment> ResolveEnrollmentAsync(TenantScheduledReportConfig? schedule,
        CancellationToken cancellationToken)
    {
        // A configuration table an admin curates, sized in tens of rows, so reading it whole is affordable.
        var mappings = (await _measureMappings.GetAllAsync(cancellationToken))
            .Where(m => !string.IsNullOrWhiteSpace(m.DQM))
            .ToList();

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unmapped = new List<string>();

        foreach (var (dqm, frequency) in Selection(schedule))
        {
            var measures = mappings
                .Where(m => m.Frequency == frequency)
                .Where(m => string.Equals(m.DQM, dqm, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Measure)
                .ToList();

            if (measures.Count == 0)
            {
                unmapped.Add($"{dqm} ({frequency})");
                continue;
            }

            selected.UnionWith(measures);
        }

        if (unmapped.Count > 0)
        {
            throw new UnmappedDqmSelectionException(unmapped);
        }

        var managed = mappings
            .Select(m => m.Measure)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new Enrollment(selected, managed);
    }

    private static IEnumerable<(string Dqm, Frequency Frequency)> Selection(
        TenantScheduledReportConfig? schedule)
    {
        if (schedule is null)
        {
            return [];
        }

        return (schedule.Daily ?? []).Select(d => (d, Frequency.Daily))
            .Concat((schedule.Weekly ?? []).Select(d => (d, Frequency.Weekly)))
            .Concat((schedule.Monthly ?? []).Select(d => (d, Frequency.Monthly)))
            .Where(s => !string.IsNullOrWhiteSpace(s.Item1));
    }

    /// <summary>
    /// The current reporting period in the facility's timezone, then the next one, as LEGLINK-913 seeds.
    /// </summary>
    private ReportingPeriod[] Periods(string facilityId, string? timeZone)
    {
        var current = _periodResolver.Resolve(facilityId, timeZone);

        return [current, current.AddMonths(1)];
    }

    /// <summary>
    /// Brings the facility's managed entries in each period into line with the selection, writing only the
    /// difference, so an unchanged selection writes nothing.
    /// </summary>
    /// <returns>The number of entries created and deleted.</returns>
    private async Task<(int Created, int Deleted)> WriteMockAsync(string facilityId, Enrollment enrollment,
        IReadOnlyList<ReportingPeriod> periods, CancellationToken cancellationToken)
    {
        var created = 0;
        var deleted = 0;

        foreach (var period in periods)
        {
            var entries = (await ReadEntriesAsync(facilityId, period, cancellationToken))
                .Where(e => enrollment.Managed.Contains(e.Measure))
                .ToList();

            // An entry the mock does not serve ("N") is stale either way: removed if the measure is dropped,
            // replaced by a reporting one if it is kept.
            var stale = entries
                .Where(e => !enrollment.Selected.Contains(e.Measure) || !IsReporting(e))
                .ToList();

            foreach (var entry in stale)
            {
                Ensure(await _mock.DeleteEntryAsync(entry.Id, cancellationToken), "delete an entry");
                deleted++;
            }

            // A measure already reporting under either component stays as it is.
            var present = entries
                .Where(e => enrollment.Selected.Contains(e.Measure) && IsReporting(e))
                .Select(e => e.Measure)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var measure in enrollment.Selected.Where(m => !present.Contains(m)))
            {
                var response = await _mock.CreateEntryAsync(new MockDmrpEntryRequest
                {
                    FacilityId = facilityId,
                    Component = ReportingComponents.Msc,
                    Measure = measure,
                    ReportingMonth = period.Month,
                    ReportingYear = period.Year,
                    IsReporting = "Y"
                }, cancellationToken);

                // 409: a concurrent save of the same facility created it first, which is what this one wanted.
                if (response.StatusCode != StatusCodes.Status409Conflict)
                {
                    Ensure(response, "create an entry");
                    created++;
                }
            }
        }

        return (created, deleted);
    }

    private async Task<List<MockDmrpEntryResponse>> ReadEntriesAsync(string facilityId, ReportingPeriod period,
        CancellationToken cancellationToken)
    {
        var entries = new List<MockDmrpEntryResponse>();

        for (var pageNumber = 1; ; pageNumber++)
        {
            var page = await _mock.SearchEntriesAsync(facilityId: facilityId,
                                                      reportingMonth: period.Month,
                                                      reportingYear: period.Year,
                                                      pageSize: PageSize,
                                                      pageNumber: pageNumber,
                                                      cancellationToken: cancellationToken);

            Ensure(page, "read the facility's entries");

            var records = page.Body?.Records ?? [];
            entries.AddRange(records);

            if (records.Count < PageSize || pageNumber >= (page.Body?.Metadata.TotalPages ?? 0))
            {
                return entries;
            }
        }
    }

    private static bool IsReporting(MockDmrpEntryResponse entry) =>
        string.Equals(entry.IsReporting, "Y", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Turns a failed mock call into the exception the facility endpoints already answer 502 for.
    /// </summary>
    private static void Ensure(LinkApiResponse response, string action)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new DmrpApiException(response.StatusCode == 0
            ? $"The Mock DMRP API could not be reached to {action}."
            : $"The Mock DMRP API refused to {action} (HTTP {response.StatusCode}).");
    }

    private static void Ensure<T>(LinkApiResponse<T> response, string action) => Ensure(response.AsUntyped(), action);

    private void LogSave(string operation, string facilityId, (int Created, int Deleted) written, int withdrawn)
    {
        _logger.LogInformation(
            "Mock DMRP write-through on facility {Operation} for {FacilityId}: {Created} entries created, " +
            "{Deleted} deleted, {Withdrawn} reporting plans withdrawn.",
            operation, facilityId.SanitizeForLog(), written.Created, written.Deleted, withdrawn);
    }
}
