import type { ReportDetail } from '../../../api/contracts';

/**
 * The NHSN measure names Report Results/Details show for a report. Prefers the exact selection
 * made when the report was requested (kept in the draft as real measure names, since Report only
 * knows the resolved dQM); falls back to the report's own real measure mapping (the facility's
 * current DMRP enrollment intersected with MeasureEval's loaded definitions, scoped to this
 * report's dQMs -- see ReportGateway.ToDetail) when that record isn't available, e.g. a report
 * generated in another session. A dQM neither source can name (the facility since unenrolled, or
 * the measure definition was removed from MeasureEval) falls back to the raw dQM id.
 */
export function friendlyMeasuresFor(
  dqmMeasures: string[],
  reportId: string,
  measureMapping: ReportDetail['measureMapping'] | undefined,
  requestedMeasuresByReportId?: Record<string, string[]>,
): string[] {
  const requested = requestedMeasuresByReportId?.[reportId];
  if (requested && requested.length > 0) {
    return requested;
  }

  const nhsnMeasureByDqm = new Map(
    (measureMapping ?? []).map((mapping) => [
      mapping.digitalQualityMeasure,
      mapping.nhsnMeasure,
    ]),
  );
  return dqmMeasures.map((dqmId) => nhsnMeasureByDqm.get(dqmId) ?? dqmId);
}

/**
 * The real dQM id behind a friendly NHSN measure name, from the report's own measure mapping.
 * Falls back to the report's own (sole) dQM when the name came from requestedMeasuresByReportId
 * and the facility's mapping has since changed enough that it no longer names this measure --
 * still the report's real dQM, just not resolvable by name anymore.
 */
export function dqmIdForMeasureName(
  name: string,
  detail: ReportDetail,
): string | undefined {
  const match = detail.measureMapping.find(
    (mapping) => mapping.nhsnMeasure === name,
  );
  if (match) {
    return match.digitalQualityMeasure;
  }
  return detail.measures.length === 1 ? detail.measures[0] : undefined;
}

/** A dQM tab's display label: the NHSN measure name(s) mapped to it, or the raw dQM id if none. */
export function dqmLabel(
  dqmId: string,
  measureMapping: ReportDetail['measureMapping'],
): string {
  const names = measureMapping
    .filter((mapping) => mapping.digitalQualityMeasure === dqmId)
    .map((mapping) => mapping.nhsnMeasure);
  return names.length > 0 ? names.join(' / ') : dqmId;
}

// The external CDC measure spec page for a dQM id. No Link service carries this URL anywhere
// (checked DMRP, MeasureEval, and the measure bundles themselves) -- external reference material
// with no Link source, so a hardcoded table is unavoidable here. Deliberately non-load-bearing:
// a dQM missing from this table still works everywhere else (tabs, filtering, export), it just
// renders as plain text instead of a link.
export const DQM_SPEC_URL_BY_ID: Record<string, string> = {
  NHSNGlycemicControlHypoglycemicInitialPopulation:
    'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalMonthlyInitialPopulation.html',
  NHSNAcuteCareHospitalMonthlyInitialPopulation:
    'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalMonthlyInitialPopulation.html',
  NHSNAcuteCareHospitalDailyInitialPopulation:
    'https://measures-ci.nhsnlink.org/Measure-NHSNAcuteCareHospitalDailyInitialPopulation.html',
  NHSNLongTermCareMonthlyInitialPopulation:
    'https://measures-ci.nhsnlink.org/Measure-NHSNLongTermCareMonthlyInitialPopulation.html',
};

// Matches the onboarding POC's measureColor(): a string hash into a fixed palette, so a given
// measure name always renders the same color without a hand-maintained name -> color map.
const MEASURE_COLOR_PALETTE = [
  '#0b5cab',
  '#7c3aed',
  '#15803d',
  '#b45309',
  '#be185d',
  '#0f766e',
  '#4338ca',
  '#a16207',
];

export function measureColor(measure: string): string {
  let hash = 0;
  for (let i = 0; i < measure.length; i++) {
    hash = (hash * 31 + measure.charCodeAt(i)) >>> 0;
  }
  return MEASURE_COLOR_PALETTE[hash % MEASURE_COLOR_PALETTE.length];
}
