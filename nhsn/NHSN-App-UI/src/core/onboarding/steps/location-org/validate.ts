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

/**
 * Two rows keying to the same value configure the same thing twice, so only the "extra" rows in a
 * colliding group come back - the row being edited carries the error, falling back to the first
 * occurrence in list order when none is. `keyOf` returns `null` for a row that isn't complete enough
 * to compare yet (left to the find-incomplete checks above), and otherwise a key compared as-is - so
 * callers trim and lower-case before returning it. Mirrors HslocStep's findDuplicateSourceCodeIndexes.
 */
function findDuplicateIndexes<T>(rows: T[], keyOf: (row: T) => string | null, editedIndex?: number): number[] {
  const indexesByKey = new Map<string, number[]>();

  rows.forEach((row, index) => {
    const key = keyOf(row);
    if (key === null) {
      return;
    }
    const indexes = indexesByKey.get(key);
    if (indexes) {
      indexes.push(index);
    } else {
      indexesByKey.set(key, [index]);
    }
  });

  const duplicates = new Set<number>();
  indexesByKey.forEach(indexes => {
    if (indexes.length < 2) {
      return;
    }
    const kept = indexes.find(index => index !== editedIndex) ?? indexes[0];
    indexes.forEach(index => {
      if (index !== kept) {
        duplicates.add(index);
      }
    });
  });

  return Array.from(duplicates).sort((a, b) => a - b);
}

/** Location Type rows that repeat another row's code + alias. */
export function findDuplicateLocationTypeIndexes(rows: LocationTypeEntry[], editedIndex?: number): number[] {
  return findDuplicateIndexes(
    rows,
    row => pairKey(row.code, row.alias),
    editedIndex
  );
}

/** Location Identifier rows that repeat another row's system + code. */
export function findDuplicateLocationIdentifierIndexes(rows: LocationIdentifierEntry[], editedIndex?: number): number[] {
  return findDuplicateIndexes(
    rows,
    row => pairKey(row.system, row.code),
    editedIndex
  );
}

export function findDuplicateManagingOrganizationIndexes(rows: string[], editedIndex?: number): number[] {
  return findDuplicateIndexes(
    rows,
    row => {
      const value = row.trim().toLowerCase();
      return value || null;
    },
    editedIndex
  );
}

function pairKey(first: string, second: string): string | null {
  const a = first.trim().toLowerCase();
  const b = second.trim().toLowerCase();
  return a && b ? `${a}|${b}` : null;
}
