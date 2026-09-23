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

function isRowBlank(row: MappingRowValues): boolean {
  return row.localValue.trim().length === 0 && !row.targetSystem && !row.targetCode;
}

/**
 * rowKeys of rows with only one side filled in - a local code with no CPT/SNOMED code chosen, or
 * vice versa. A row where NEITHER side is filled in is unused scaffolding, not incomplete data -
 * see `pruneEmptyGroups`, which is what drops those before Continue is blocked on this.
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

/**
 * groupKeys of groups with a blank Encounter.type Code System despite having mapping rows to
 * save under it - there's no code system for those rows to attach to. Call after
 * `pruneEmptyGroups` so a still-blank group with no mappings (unused scaffolding, not a mistake)
 * isn't flagged.
 */
export function findMissingCodeSystemGroupKeys(groups: CodeSystemGroupValues[]): string[] {
  return groups.filter(group => !group.codeSystem.trim() && group.mappings.length > 0).map(group => group.groupKey);
}

/**
 * Drops mapping rows nobody has typed anything into, then drops any group left with a blank
 * code system and no mappings - unused scaffolding from "+ Add Code System" / "+ Add Mapping"
 * that a user never filled in or decided against, not data worth blocking Continue over.
 * Call this before findMissingCodeSystemGroupKeys/findIncompleteRowKeys decide whether to block.
 */
export function pruneEmptyGroups<T extends CodeSystemGroupValues>(groups: T[]): T[] {
  return groups
    .map(group => ({...group, mappings: group.mappings.filter(row => !isRowBlank(row))}))
    .filter(group => group.codeSystem.trim().length > 0 || group.mappings.length > 0);
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
    if (seen.has(codeSystem)) {
      duplicates.push(group.groupKey);
      return;
    }
    seen.add(codeSystem);
  });

  return duplicates;
}
