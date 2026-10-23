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
 * rowKeys of rows missing either side - a local code with no CPT/SNOMED code chosen, vice versa,
 * or neither. A blank row isn't silently dropped on Continue; it has its own Remove control.
 */
export function findIncompleteRowKeys(groups: CodeSystemGroupValues[]): string[] {
  const incomplete: string[] = [];
  groups.forEach(group => {
    group.mappings.forEach(row => {
      const hasLocal = row.localValue.trim().length > 0;
      const hasTarget = Boolean(row.targetSystem && row.targetCode);
      if (!hasLocal || !hasTarget) {
        incomplete.push(row.rowKey);
      }
    });
  });
  return incomplete;
}

/**
 * groupKeys of groups with a blank Encounter.type Code System - whether or not they have mapping
 * rows under them yet.
 */
export function findMissingCodeSystemGroupKeys(groups: CodeSystemGroupValues[]): string[] {
  return groups.filter(group => !group.codeSystem.trim()).map(group => group.groupKey);
}

/**
 * groupKeys of groups whose codeSystem (trimmed; blank counts as a value like any other) repeats
 * an earlier group's - flags the repeat only, not the group it repeats. Two groups sharing a
 * value is more than confusing UI: the draft round-trips code systems as a flat, system-keyed
 * list (see EncounterStep's flattenGroups/buildGroups), which can't tell same-valued groups
 * apart, so their mapping rows collapse into one group's worth on the next Continue/Back cycle.
 */
export function findDuplicateCodeSystemIndexes(groups: CodeSystemGroupValues[]): string[] {
  const seen = new Set<string>();
  const duplicates: string[] = [];

  groups.forEach(group => {
    const codeSystem = group.codeSystem.trim();
    if (!codeSystem) {
      return;
    }
    if (seen.has(codeSystem)) {
      duplicates.push(group.groupKey);
      return;
    }
    seen.add(codeSystem);
  });

  return duplicates;
}
