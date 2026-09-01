using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// What one facility's sync did.
    /// </summary>
    /// <param name="Recorded">Enrollments stored that Link had no row for.</param>
    /// <param name="Reinstated">Enrollments DMRP returned that Link had recorded as withdrawn.</param>
    /// <param name="Withdrawn">Rows marked not reporting because DMRP stopped returning them.</param>
    /// <param name="Unmapped">
    /// Of the enrollments seen, how many name a measure Link has no measure mapping for. They are
    /// recorded and schedule nothing until an admin maps them.
    /// </param>
    public sealed record DmrpSyncResult(int Recorded, int Reinstated, int Withdrawn, int Unmapped)
    {
        public static readonly DmrpSyncResult Nothing = new(0, 0, 0, 0);
    }

    /// <summary>
    /// Brings Link's record of a facility's reporting plan into line with what the DMRP API says.
    /// </summary>
    public interface IDmrpReportingPlanSync
    {
        /// <summary>
        /// Reads the facility's plan for a period from DMRP and writes what changed.
        /// </summary>
        /// <exception cref="DmrpApiException">The API could not be read; nothing is written.</exception>
        Task<DmrpSyncResult> SyncAsync(string facilityId, int month, int year,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Reads DMRP, resolves each measure to a mapping where Link has one, and reconciles the rows.
    /// </summary>
    /// <remarks>
    /// Writes through the repository rather than the manager. The manager validates one plan at a
    /// time and re-checks that the facility exists for each, which is the right shape for an
    /// operator submitting a row and the wrong one for a batch that came from DMRP itself - the
    /// facility was already known when the sync was asked for, and the measures are DMRP's rather
    /// than a caller's to get wrong.
    /// <para>
    /// Nothing calls this yet. When and how often a facility is synced is a separate decision, and
    /// the onboarding-versus-polling question behind it is not settled.
    /// </para>
    /// </remarks>
    public sealed class DmrpReportingPlanSync : IDmrpReportingPlanSync
    {
        private readonly IDmrpApiClient _client;
        private readonly IEntityRepository<FacilityReportingPlan> _plans;
        private readonly IEntityRepository<MeasureMapping> _measureMappings;
        private readonly ILogger<DmrpReportingPlanSync> _logger;

        public DmrpReportingPlanSync(IDmrpApiClient client,
            IEntityRepository<FacilityReportingPlan> plans,
            IEntityRepository<MeasureMapping> measureMappings,
            ILogger<DmrpReportingPlanSync> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _plans = plans ?? throw new ArgumentNullException(nameof(plans));
            _measureMappings = measureMappings ?? throw new ArgumentNullException(nameof(measureMappings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DmrpSyncResult> SyncAsync(string facilityId, int month, int year,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(facilityId);

            var entries = await _client.GetReportingPlanAsync(facilityId, month, year, cancellationToken);

            if (entries.Count == 0)
            {
                // An empty plan and a facility DMRP has nothing to say about look identical - there
                // is no negative representation to tell them apart - so nothing is written. The
                // cost is that a facility withdrawing from everything at once is not detected;
                // withdrawing from some of what it reports still is.
                _logger.LogInformation(
                    "DMRP returned no enrollments for facility {FacilityId} for {Month}/{Year}; nothing was changed.",
                    facilityId.SanitizeForLog(), month, year);

                return DmrpSyncResult.Nothing;
            }

            var mappings = await ResolveMappingsAsync(entries, cancellationToken);

            var existing = await _plans.FindAsync(p => p.FacilityId == facilityId
                && p.ReportingMonth == month
                && p.ReportingYear == year, cancellationToken);

            var recorded = 0;
            var reinstated = 0;
            var unmapped = 0;

            foreach (var entry in entries)
            {
                mappings.TryGetValue(entry.Measure, out var mappingId);

                if (mappingId is null)
                {
                    unmapped++;
                }

                var row = existing.FirstOrDefault(p => Matches(p, entry));

                if (row is null)
                {
                    await _plans.AddAsync(new FacilityReportingPlan
                    {
                        FacilityId = facilityId,
                        Component = entry.Component,
                        Measure = entry.Measure,
                        MeasureMappingId = mappingId,
                        ReportingMonth = entry.ReportingMonth,
                        ReportingYear = entry.ReportingYear,
                        IsReporting = true
                    }, cancellationToken);

                    recorded++;
                    continue;
                }

                if (!row.IsReporting)
                {
                    row.IsReporting = true;
                    reinstated++;
                }

                // A measure mapped since the last sync fills in here. The reverse is not done: a
                // mapping is not cleared because this run could not resolve it, since that would
                // undo an admin's work on the strength of a lookup.
                if (row.MeasureMappingId is null && mappingId is not null)
                {
                    row.MeasureMappingId = mappingId;
                }

                _plans.Update(row);
            }

            var withdrawn = Withdraw(entries, existing);

            await _plans.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Synced facility {FacilityId} for {Month}/{Year}: {Recorded} recorded, {Reinstated} reinstated, " +
                "{Withdrawn} withdrawn, {Unmapped} awaiting a measure mapping.",
                facilityId.SanitizeForLog(), month, year, recorded, reinstated, withdrawn, unmapped);

            return new DmrpSyncResult(recorded, reinstated, withdrawn, unmapped);
        }

        /// <summary>
        /// Marks rows DMRP no longer returns as no longer reported.
        /// </summary>
        /// <remarks>
        /// Scoped to the components that answered. DMRP says what a facility reports by returning
        /// it, so absence is the only way a withdrawal ever appears - but absence of a whole
        /// component is indistinguishable from that component having nothing to say, and treating
        /// the two alike would withdraw a facility's medicine plan because its patient-safety plan
        /// came back empty.
        /// </remarks>
        private int Withdraw(IReadOnlyList<DmrpReportingPlanEntry> entries, List<FacilityReportingPlan> existing)
        {
            var componentsThatAnswered = entries
                .Select(e => e.Component)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var withdrawn = 0;

            foreach (var row in existing.Where(p => p.IsReporting
                && componentsThatAnswered.Contains(p.Component)
                && !entries.Any(e => Matches(p, e))))
            {
                // Set false rather than deleted: the row is the record of what DMRP said and when
                // it changed, and the facility's own view shows it as history.
                row.IsReporting = false;
                _plans.Update(row);

                withdrawn++;
            }

            return withdrawn;
        }

        /// <summary>
        /// The measure mapping id for each measure DMRP returned, where Link has one.
        /// </summary>
        /// <remarks>
        /// Compared case-insensitively in one read. Matching in C# rather than leaning on the
        /// database's collation keeps a measure seeded as "hob" from being invisible to a plan that
        /// names it "HOB" - a difference no local run against SQL Server's default case-insensitive
        /// collation would ever reveal. Reading the whole table to do it is affordable because it
        /// is a configuration table an admin curates, sized in tens of rows.
        /// </remarks>
        private async Task<Dictionary<string, string?>> ResolveMappingsAsync(
            IReadOnlyList<DmrpReportingPlanEntry> entries, CancellationToken cancellationToken)
        {
            var measures = entries.Select(e => e.Measure).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var candidates = await _measureMappings.GetAllAsync(cancellationToken);

            var byMeasure = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in candidates.Where(m => measures.Contains(m.Measure)))
            {
                byMeasure.TryAdd(mapping.Measure, mapping.Id);
            }

            return byMeasure;
        }

        private static bool Matches(FacilityReportingPlan row, DmrpReportingPlanEntry entry) =>
            string.Equals(row.Component, entry.Component, StringComparison.OrdinalIgnoreCase)
            && string.Equals(row.Measure, entry.Measure, StringComparison.OrdinalIgnoreCase)
            && row.ReportingMonth == entry.ReportingMonth
            && row.ReportingYear == entry.ReportingYear;
    }
}
