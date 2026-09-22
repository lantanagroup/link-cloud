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
  Two rows mapping the same local code to different HSLOC codes is ambiguous —
 * the save path (HslocMappingService.SaveAsync) groups incoming rows by local code
 * case-insensitively and keeps only one, so an unflagged duplicate here would silently lose
 * a row on save. Blank sourceCode values are excluded; findIncompleteRowIndexes already
 * flags those. Both rows in a colliding pair are returned, not just the second.
 */
export function findDuplicateSourceCodeIndexes(rows: MappingRowValues[]): number[] {
  const firstIndexByCode = new Map<string, number>();
  const duplicates = new Set<number>();

  rows.forEach((row, index) => {
    const code = row.sourceCode.trim().toLowerCase();
    if (!code) {
      return;
    }
    const firstIndex = firstIndexByCode.get(code);
    if (firstIndex === undefined) {
      firstIndexByCode.set(code, index);
      return;
    }
    duplicates.add(firstIndex);
    duplicates.add(index);
  });

  return Array.from(duplicates).sort((a, b) => a - b);
}
