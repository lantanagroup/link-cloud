export interface MappingRowValues {
  rowKey: string;
  localValue: string;
  targetSystem: string;
  targetCode: string;
}

export interface CodeSystemGroupValues {
  groupKey: string;
  codeSystem: string;
  mappings: MappingRowValues[];
}

/**
 * Non-blocking: this step never blocks Continue on incomplete mappings (POC
 * parity — encounterMappingAcknowledged is set unconditionally). Returns the
 * rowKeys of rows with only one side filled in, so the row can be flagged.
 */
export function findIncompleteRowKeys(groups: CodeSystemGroupValues[]): string[] {
  const incomplete: string[] = [];
  groups.forEach(group => {
    group.mappings.forEach(row => {
      const hasLocal = row.localValue.trim().length > 0;
      const hasTarget = Boolean(row.targetSystem && row.targetCode);
      if (hasLocal !== hasTarget) {
        incomplete.push(row.rowKey);
      }
    });
  });
  return incomplete;
}
