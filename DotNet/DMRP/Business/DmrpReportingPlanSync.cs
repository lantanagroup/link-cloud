using LantanaGroup.Link.DMRP.Api;
using LantanaGroup.Link.DMRP.Data.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;

namespace LantanaGroup.Link.DMRP.Business
{
    /// <summary>
    /// The measure mapping a measure resolved to, and whether that mapping names a dQM.
    /// </summary>
    /// <remarks>
    /// Every measure resolves to a row now, because the sync records one for a measure it cannot map.
    /// So "unmapped" stopped meaning "no row" and started meaning "a row with no dQM", and the two
    /// have to be told apart: the plan links to the row either way, but only a row with a dQM
    /// schedules anything.
    /// </remarks>
    internal sealed record MeasureMappingRef(string Id, bool HasDqm);

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

            // Two refreshes for the same facility and period can both read no row and both try to
            // insert it; the unique index lets one through. That is a lost race rather than a
            // fault -- the other run recorded exactly what this one meant to -- so the answer is to
            // read the table again and reconcile against what is now there. Retried once: a second
            // conflict is no longer a race with a live caller and should surface.
            //
            // The alternative, a lock keyed on facility and period, only holds inside one process.
            // It would look like a fix and quietly stop being one the day Tenant runs more than one
            // instance, which is the worst way for this to fail.
            for (var attempt = 1; ; attempt++)
            {
                var inserted = new List<FacilityReportingPlan>();

                try
                {
                    return await ReconcileAsync(facilityId, month, year, entries, mappings, inserted,
                        cancellationToken);
                }
                catch (Exception exception) when (attempt == 1 && UniquePeriodViolation.Matches(exception))
                {
                    _logger.LogInformation(exception,
                        "A concurrent sync recorded part of facility {FacilityId}'s plan for {Month}/{Year} first; "
                        + "reconciling against what it wrote.",
                        facilityId.SanitizeForLog(), month, year);

                    // Removing an entity that is still Added detaches it rather than scheduling a
                    // delete, which is what lets the next attempt start from a clean context. Rows
                    // this attempt modified stay tracked and are simply re-applied, which is safe
                    // because every change it makes is idempotent.
                    foreach (var row in inserted)
                    {
                        _plans.Remove(row);
                    }
                }
            }
        }

        private async Task<DmrpSyncResult> ReconcileAsync(string facilityId, int month, int year,
            IReadOnlyList<DmrpReportingPlanEntry> entries,
            IReadOnlyDictionary<string, MeasureMappingRef> mappings,
            List<FacilityReportingPlan> inserted,
            CancellationToken cancellationToken)
        {
            // Every period the response actually speaks about, not just the one asked for. The client
            // keeps an entry's own month and year in preference to the requested ones on purpose, so
            // a response can carry a period we did not ask about -- and loading only the requested
            // period would leave such an entry unable to match anything, inserted fresh on every
            // refresh until it collided with the unique index.
            var periods = entries
                .Select(e => (Month: e.ReportingMonth, Year: e.ReportingYear))
                .Append((Month: month, Year: year))
                .ToHashSet();

            var months = periods.Select(period => period.Month).Distinct().ToList();
            var years = periods.Select(period => period.Year).Distinct().ToList();

            // The predicate is the cross product of those months and years, which is as close as a
            // translatable query gets; the periods actually present are selected out of it here.
            var candidates = await _plans.FindAsync(p => p.FacilityId == facilityId
                && months.Contains(p.ReportingMonth)
                && years.Contains(p.ReportingYear), cancellationToken);

            var existing = candidates
                .Where(p => periods.Contains((p.ReportingMonth, p.ReportingYear)))
                .ToList();

            var recorded = 0;
            var reinstated = 0;
            var unmapped = 0;

            // What this run has already written. Two entries naming the same enrollment would
            // otherwise be added twice and break the unique index on save -- and because the whole
            // sync commits at once, that would throw away every legitimate change alongside them.
            var added = new HashSet<(string Component, string Measure, int Month, int Year)>();

            foreach (var entry in entries)
            {
                mappings.TryGetValue(entry.Measure, out var mapping);

                // Counted on the dQM, not on the row: the sync records a mapping for every measure it
                // sees, so a missing row no longer means a measure Link cannot schedule.
                if (mapping is null || !mapping.HasDqm)
                {
                    unmapped++;
                }

                var row = existing.FirstOrDefault(p => Matches(p, entry));

                if (row is null)
                {
                    if (!added.Add((entry.Component.ToUpperInvariant(), entry.Measure.ToUpperInvariant(),
                            entry.ReportingMonth, entry.ReportingYear)))
                    {
                        // The same enrollment listed twice in one response. Recording it once is the
                        // whole of the answer; counting it twice would report work that did not happen.
                        _logger.LogDebug(
                            "DMRP listed {Measure} for {Month}/{Year} more than once; the repeat was ignored.",
                            entry.Measure.SanitizeForLog(), entry.ReportingMonth, entry.ReportingYear);

                        continue;
                    }

                    inserted.Add(await _plans.AddAsync(new FacilityReportingPlan
                    {
                        FacilityId = facilityId,
                        Component = entry.Component,
                        Measure = entry.Measure,
                        MeasureMappingId = mapping?.Id,
                        ReportingMonth = entry.ReportingMonth,
                        ReportingYear = entry.ReportingYear,
                        IsReporting = true
                    }, cancellationToken));

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
                // Only ever fills a gap. Completing a measure sets the dQM on the row the sync
                // already recorded, so the id a plan points at does not change and the facilities
                // enrolled in that measure pick the dQM up without being touched. This is here for
                // rows written before the sync recorded mappings of its own, which have no id at all.
                if (row.MeasureMappingId is null && mapping is not null)
                {
                    row.MeasureMappingId = mapping.Id;
                }

                // Deliberately not marked modified: the rows are tracked, so change detection picks
                // up only the ones a branch above actually altered.
            }

            // Only the period that was asked about. Absence is how DMRP says "withdrawn", but it can
            // only mean that for the period the question named -- a row in some other period was
            // never in scope of this answer, so its absence says nothing at all.
            var withdrawn = Withdraw(entries,
                existing.Where(p => p.ReportingMonth == month && p.ReportingYear == year).ToList());

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
        /// <summary>
        /// The measure mapping id for every measure in the response, recording one for any measure
        /// Link has no mapping for.
        /// </summary>
        /// <remarks>
        /// The measure mappings table is the source of truth for what Link can schedule, so a measure
        /// DMRP reports belongs in it whether or not anyone has mapped it to a dQM yet. Recording it
        /// with no dQM puts it in front of the administrator who can complete it, on the page they
        /// already maintain, rather than leaving it visible only as an absence. Nothing schedules
        /// against a mapping with no dQM, so an incomplete row changes no reporting behaviour.
        /// </remarks>
        private async Task<Dictionary<string, MeasureMappingRef>> ResolveMappingsAsync(
            IReadOnlyList<DmrpReportingPlanEntry> entries, CancellationToken cancellationToken)
        {
            var measures = entries.Select(e => e.Measure).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var byMeasure = await ReadMappingsAsync(measures, cancellationToken);

            var missing = measures.Where(measure => !byMeasure.ContainsKey(measure)).ToList();

            if (missing.Count == 0)
            {
                return byMeasure;
            }

            var recorded = new List<MeasureMapping>(missing.Count);

            foreach (var measure in missing)
            {
                // Frequency stays Adhoc rather than becoming nullable: it already means "belongs to no
                // reporting frequency", which is exactly what a measure with no dQM schedules.
                var placeholder = new MeasureMapping
                {
                    Measure = measure,
                    DQM = null,
                    Frequency = Frequency.Adhoc
                };

                await _measureMappings.AddAsync(placeholder, cancellationToken);

                recorded.Add(placeholder);
                byMeasure[measure] = new MeasureMappingRef(placeholder.Id, HasDqm: false);
            }

            try
            {
                // Saved on its own rather than with the reporting plans: a mapping is worth keeping
                // whatever becomes of this run's plans, and a lost race here says nothing about them.
                await _measureMappings.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (DuplicateMeasureMapping.Matches(exception))
            {
                _logger.LogInformation(exception,
                    "A concurrent sync recorded a measure mapping first; reading back what it wrote.");

                // Removing an entity that is still Added detaches it rather than scheduling a delete,
                // which leaves the context clean for the read below.
                foreach (var placeholder in recorded)
                {
                    _measureMappings.Remove(placeholder);
                }

                return await ReadMappingsAsync(measures, cancellationToken);
            }

            _logger.LogInformation(
                "Recorded {Count} measure(s) DMRP reported that Link has no dQM for: {Measures}. "
                + "They are on the measure mappings page awaiting a dQM and schedule nothing until one is set.",
                missing.Count, string.Join(", ", missing.Select(measure => measure.SanitizeForLog())));

            return byMeasure;
        }

        private async Task<Dictionary<string, MeasureMappingRef>> ReadMappingsAsync(
            IReadOnlySet<string> measures, CancellationToken cancellationToken)
        {
            var candidates = await _measureMappings.GetAllAsync(cancellationToken);

            var byMeasure = new Dictionary<string, MeasureMappingRef>(StringComparer.OrdinalIgnoreCase);

            // At most one row per measure, enforced by the unique index, so there is nothing to
            // choose between: whatever is there is the mapping, complete or not.
            foreach (var mapping in candidates.Where(m => measures.Contains(m.Measure)))
            {
                byMeasure[mapping.Measure] =
                    new MeasureMappingRef(mapping.Id, HasDqm: !string.IsNullOrWhiteSpace(mapping.DQM));
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
