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
