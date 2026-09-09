/**
 * The one place patient ids are added to the report.
 *
 * All four tabs funnel through `addPatientIds`, so the limit and the
 * duplicate rule are enforced once rather than four times — the POC checks
 * them per method and the manual path drifts from the CSV path as a result.
 */

/**
 * Tenant validates every id against the facility's FHIR server before the
 * report runs, which is what makes a small ceiling the right one.
 */
export const PATIENT_ID_LIMIT = 10;

export interface AddPatientIdsResult {
  next: string[];
  added: number;
  skipped: number;
}

/**
 * Appends what fits, in the order given, keeping the first occurrence of a
 * duplicate. Comparison is exact: a FHIR id is case-sensitive, so `abc` and
 * `ABC` are two different patients and collapsing them would silently drop one.
 */
export function addPatientIds(current: readonly string[], incoming: readonly string[]): AddPatientIdsResult {
  const next = [...current];
  const seen = new Set(current);
  let added = 0;
  let skipped = 0;

  for (const raw of incoming) {
    const id = raw.trim();
    if (!id) {
      continue;
    }
    if (seen.has(id) || next.length >= PATIENT_ID_LIMIT) {
      skipped += 1;
      continue;
    }
    seen.add(id);
    next.push(id);
    added += 1;
  }

  return added === 0 ? {next: [...current], added, skipped} : {next, added, skipped};
}

export function isAtPatientIdLimit(ids: readonly string[] | undefined): boolean {
  return (ids?.length ?? 0) >= PATIENT_ID_LIMIT;
}
