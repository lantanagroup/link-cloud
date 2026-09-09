/**
 * Stand-in measures: the reporting step's measure picker is hardcoded to
 * these for now rather than reading a facility's reporting plan.
 *
 * They are **not** facility data — the id is a made-up placeholder, not a
 * digital quality measure Tenant knows about, so it is never what gets sent
 * on request. `digitalQualityMeasure` is the real dQM the placeholder stands
 * in for; `toDigitalQualityMeasures` resolves selected ids to it before a
 * report request goes out, since Tenant validates every report type against
 * MeasureEval and would refuse (in fact 500s on) the placeholder id itself.
 * All five currently point at the same dQM -- MeasureEval has exactly one
 * definition seeded (`NHSNAcuteCareHospitalMonthlyInitialPopulation`) in this
 * environment, and a report request naming any other dQM 500s the same way.
 *
 * The names are stand-in *data*, not UI copy, which is why they are literals
 * here rather than i18n keys — the real ones arrive from the service
 * untranslated too. The "(placeholder)" marking around them is copy, and lives
 * in the bundle.
 *
 * Delete this file once the reporting step reads a facility's real plan.
 */
export interface PlaceholderMeasure {
  id: string;
  name: string;
  /** The digital quality measure the real mapping would carry, for reference. */
  digitalQualityMeasure: string;
}

/**
 * Ids carry the prefix so one appearing in a request, a log or a support ticket
 * is self-identifying rather than looking like a measure that has gone missing.
 * They are per-measure rather than per-dQM so the picker offers five distinct
 * chips even though every one currently resolves to the same dQM.
 */
export const PLACEHOLDER_MEASURES: readonly PlaceholderMeasure[] = [
  {
    id: 'PLACEHOLDER-GLYCEMIC-CONTROL',
    name: 'Glycemic Control',
    digitalQualityMeasure: 'NHSNAcuteCareHospitalMonthlyInitialPopulation'
  },
  {
    id: 'PLACEHOLDER-RESPIRATORY-PATHOGENS',
    name: 'Respiratory Pathogens Surveillance (RPS)',
    digitalQualityMeasure: 'NHSNAcuteCareHospitalMonthlyInitialPopulation'
  },
  {
    id: 'PLACEHOLDER-ADULT-SEPSIS',
    name: 'Adult Sepsis Bacteria & Fungemia',
    digitalQualityMeasure: 'NHSNAcuteCareHospitalMonthlyInitialPopulation'
  },
  {
    id: 'PLACEHOLDER-C-DIFFICILE',
    name: 'C. Difficile Infection',
    digitalQualityMeasure: 'NHSNAcuteCareHospitalMonthlyInitialPopulation'
  },
  {
    id: 'PLACEHOLDER-ANTIMICROBIAL-USE',
    name: 'Antimicrobial Use and Resistance (AU/AR)',
    digitalQualityMeasure: 'NHSNAcuteCareHospitalMonthlyInitialPopulation'
  }
];

/**
 * Selected placeholder ids to the dQMs a report request carries. Distinct
 * because every placeholder currently shares the one seeded dQM — Tenant
 * refuses a request that names one twice.
 */
export function toDigitalQualityMeasures(ids: readonly string[]): string[] {
  const byId = new Map(PLACEHOLDER_MEASURES.map(measure => [measure.id, measure.digitalQualityMeasure]));
  return [...new Set(ids.map(id => byId.get(id)).filter((dqm): dqm is string => Boolean(dqm)))];
}
