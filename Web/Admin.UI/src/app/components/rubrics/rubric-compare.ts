import { IRubricCheck, IRubricDefinition } from '../../interfaces/validation/rubric.interface';
import {
  checkSettings,
  checkSummary,
  checkTypeLabel,
  dimensionLabel,
  isCheckEnabled,
  isCombinedCheck,
  orderedChecks,
  scoringLabel,
  settingLabel,
  severityLabel
} from './rubric-display';

// What differs between two versions of a rubric, worded as the screens word a version: a reviewer reads
// what changed, not two files side by side.

/** One thing that differs. An empty side means it is not set on that side. */
export interface RubricChange {
  label: string;
  before: string;
  after: string;
}

/** A check both versions have, and what differs in it. */
export interface RubricCheckChange {
  id: string;
  changes: RubricChange[];
}

export interface RubricComparison {
  /** The rubric's own settings that differ: title, owner, dimensions, scoring, where it applies. */
  settings: RubricChange[];
  /** Checks only the newer side has, in the order they run. */
  added: IRubricCheck[];
  /** Checks only the older side has, in the order they ran. */
  removed: IRubricCheck[];
  /** Checks both have that differ, in the newer side's order. */
  changed: RubricCheckChange[];
  /** How many checks both have exactly alike. */
  unchangedCount: number;
  /** True when the checks both sides have run in a different order. */
  reordered: boolean;
  /** True when nothing at all differs. */
  identical: boolean;
}

function change(label: string, before: string, after: string): RubricChange[] {
  return before === after ? [] : [{ label, before, after }];
}

const list = (values?: string[] | null): string => (values ?? []).join(', ');

/** The child checks of a combined check, one line each, as text that differs when any of them does. */
function childrenText(check: IRubricCheck): string {
  if (!isCombinedCheck(check)) {
    return '';
  }
  return (check.parameters?.checks ?? [])
    .map(child => {
      const nested = isCombinedCheck(child) ? ` [${childrenText(child).replace(/\n/g, '; ')}]` : '';
      return `${child.id} (${checkTypeLabel(child.type)}): ${checkSummary(child)}${nested}`;
    })
    .join('\n');
}

function compareCheck(before: IRubricCheck, after: IRubricCheck): RubricChange[] {
  const changes: RubricChange[] = [
    ...change('Type', checkTypeLabel(before.type), checkTypeLabel(after.type)),
    ...change('Dimension', dimensionLabel(before.dimension), dimensionLabel(after.dimension)),
    ...change('Severity', severityLabel(before.severityOverride), severityLabel(after.severityOverride)),
    ...change('Enabled', isCheckEnabled(before) ? 'Yes' : 'No', isCheckEnabled(after) ? 'Yes' : 'No')
  ];

  const beforeSettings = new Map(checkSettings(before).map(setting => [setting.key, setting.value]));
  const afterSettings = new Map(checkSettings(after).map(setting => [setting.key, setting.value]));
  // The newer side's settings in its order, then any only the older side has.
  const keys = [...afterSettings.keys(), ...[...beforeSettings.keys()].filter(key => !afterSettings.has(key))];
  for (const key of keys) {
    changes.push(...change(settingLabel(key), beforeSettings.get(key) ?? '', afterSettings.get(key) ?? ''));
  }

  changes.push(...change('Child checks', childrenText(before), childrenText(after)));
  return changes;
}

/** What differs going from `before` to `after`. The rubric id and the version number are not compared. */
export function compareDefinitions(before: IRubricDefinition, after: IRubricDefinition): RubricComparison {
  const settings: RubricChange[] = [
    ...change('Title', before.title ?? '', after.title ?? ''),
    ...change('Owner', before.owner ?? '', after.owner ?? ''),
    ...change('Dimensions', list(before.dimensions?.map(dimensionLabel)), list(after.dimensions?.map(dimensionLabel))),
    ...change('Scoring', scoringLabel(before.scoringPolicy), scoringLabel(after.scoringPolicy)),
    ...change('Resource types', list(before.applicableContext?.fhirResources), list(after.applicableContext?.fhirResources)),
    ...change('Workflow tags', list(before.applicableContext?.workflowTags), list(after.applicableContext?.workflowTags))
  ];

  const beforeChecks = orderedChecks(before);
  const afterChecks = orderedChecks(after);
  const beforeById = new Map(beforeChecks.map(check => [check.id, check]));
  const afterIds = new Set(afterChecks.map(check => check.id));

  const added: IRubricCheck[] = [];
  const changed: RubricCheckChange[] = [];
  let unchangedCount = 0;
  for (const check of afterChecks) {
    const earlier = beforeById.get(check.id);
    if (!earlier) {
      added.push(check);
      continue;
    }
    const changes = compareCheck(earlier, check);
    if (changes.length > 0) {
      changed.push({ id: check.id, changes });
    } else {
      unchangedCount++;
    }
  }
  const removed = beforeChecks.filter(check => !afterIds.has(check.id));

  // Order is compared among the checks both sides have: adding or removing one moves nothing.
  const sharedBefore = beforeChecks.filter(check => afterIds.has(check.id)).map(check => check.id);
  const sharedAfter = afterChecks.filter(check => beforeById.has(check.id)).map(check => check.id);
  const reordered = sharedBefore.some((id, index) => id !== sharedAfter[index]);

  return {
    settings,
    added,
    removed,
    changed,
    unchangedCount,
    reordered,
    identical: settings.length === 0 && added.length === 0 && removed.length === 0 && changed.length === 0 && !reordered
  };
}
