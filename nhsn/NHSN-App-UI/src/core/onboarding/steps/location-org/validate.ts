import type {LocationIdentifierEntry, LocationTypeEntry} from '../../types';

/** A row counts as incomplete unless both its fields are filled - same rule HSLOC's mapping rows
 *  already use, so a row added but not yet finished (including a still-blank one) blocks Continue
 *  rather than silently saving half a pair Data Acquisition can't do anything with. */
export function findIncompleteLocationTypeIndexes(rows: LocationTypeEntry[]): number[] {
  const incomplete: number[] = [];
  rows.forEach((row, index) => {
    if (!(row.code.trim() && row.alias.trim())) {
      incomplete.push(index);
    }
  });
  return incomplete;
}

export function findIncompleteLocationIdentifierIndexes(rows: LocationIdentifierEntry[]): number[] {
  const incomplete: number[] = [];
  rows.forEach((row, index) => {
    if (!(row.system.trim() && row.code.trim())) {
      incomplete.push(index);
    }
  });
  return incomplete;
}

/** Indexes of every row whose values repeat an earlier row's (case-insensitive, trimmed). The
 *  first occurrence is left unflagged - it's the repeats that need removing. A row with any blank
 *  value is skipped; that's a required-field problem, not a duplicate. */
function findDuplicateIndexes<T>(rows: T[], values: (row: T) => string[]): number[] {
  const seen = new Set<string>();
  const duplicates: number[] = [];
  rows.forEach((row, index) => {
    const parts = values(row).map(value => value.trim().toLowerCase());
    if (parts.some(part => !part)) {
      return;
    }
    const key = JSON.stringify(parts);
    if (seen.has(key)) {
      duplicates.push(index);
    } else {
      seen.add(key);
    }
  });
  return duplicates;
}

export function findDuplicateManagingOrgIndexes(ids: string[]): number[] {
  return findDuplicateIndexes(ids, id => [id]);
}

export function findDuplicateLocationTypeIndexes(rows: LocationTypeEntry[]): number[] {
  return findDuplicateIndexes(rows, row => [row.code, row.alias]);
}

export function findDuplicateLocationIdentifierIndexes(rows: LocationIdentifierEntry[]): number[] {
  return findDuplicateIndexes(rows, row => [row.system, row.code]);
}
