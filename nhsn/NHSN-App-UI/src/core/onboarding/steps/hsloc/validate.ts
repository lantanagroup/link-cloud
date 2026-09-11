export interface MappingRowValues {
  sourceDisplay: string;
  sourceCode: string;
  hslocCode: string;
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
