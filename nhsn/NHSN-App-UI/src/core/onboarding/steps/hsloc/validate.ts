import type {HslocMapping} from '../../../api/contracts';
import type {FacilityDraft} from '../../types';

export interface MappingRowValues {
  sourceDisplay: string;
  sourceCode: string;
  hslocCode: string;
}

export function isRowBlank(row: MappingRowValues): boolean {
  return !row.sourceDisplay.trim() && !row.sourceCode.trim() && !row.hslocCode.trim();
}

export function isRowComplete(row: MappingRowValues): boolean {
  return Boolean(row.sourceDisplay.trim() && row.sourceCode.trim() && row.hslocCode.trim());
}

export function findIncompleteRowIndexes(rows: MappingRowValues[]): number[] {
  const incomplete: number[] = [];
  rows.forEach((row, index) => {
    const hasSourceDisplay = row.sourceDisplay.trim().length > 0;
    const hasSourceCode = row.sourceCode.trim().length > 0;
    const hasHslocCode = row.hslocCode.trim().length > 0;
    if (!(hasSourceDisplay && hasSourceCode && hasHslocCode)) {
      incomplete.push(index);
    }
  });
  return incomplete;
}

/**
 * Two rows mapping the same local code to different HSLOC codes is ambiguous — the save path
 * (HslocMappingService.SaveAsync) groups incoming rows by local code case-insensitively and keeps
 * only one, so an unflagged duplicate here would silently lose a row on save. Blank sourceCode
 * values are excluded; findIncompleteRowIndexes already flags those. Only the "extra" rows in a
 * colliding group come back - the row the facility is actively editing is treated as the one that
 * caused the collision and gets the error, leaving the other, already-there row clean (falls back
 * to the first row in list order when no row is actively being edited, e.g. on initial load).
 * Mirrors CensusStep's findDuplicatePatientListIdKeys.
 */
export function findDuplicateSourceCodeIndexes(rows: MappingRowValues[], editedIndex?: number): number[] {
  const indexesByCode = new Map<string, number[]>();

  rows.forEach((row, index) => {
    const code = row.sourceCode.trim().toLowerCase();
    if (!code) {
      return;
    }
    const indexes = indexesByCode.get(code);
    if (indexes) {
      indexes.push(index);
    } else {
      indexesByCode.set(code, [index]);
    }
  });

  const duplicates = new Set<number>();
  indexesByCode.forEach(indexes => {
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

function toRowValues(mapping: HslocMapping): MappingRowValues {
  return {sourceDisplay: mapping.sourceDisplay ?? '', sourceCode: mapping.sourceCode, hslocCode: mapping.hslocCode};
}

/**
 * Mirrors HslocStep.validateStep: at least one mapping, none left incomplete (a manual-upload
 * import can land a row with an unresolved hslocCode - see ManualUploadStep), and no source code
 * mapped twice.
 */
export function isHslocComplete(draft: FacilityDraft): boolean {
  const mappings = draft.hsloc.mappings ?? [];
  if (mappings.length === 0) {
    return false;
  }
  const rows = mappings.map(toRowValues);
  return rows.every(isRowComplete) && findDuplicateSourceCodeIndexes(rows).length === 0;
}
