import {
  IRubricCheck,
  IRubricDefinition,
  IRubricScoringPolicy,
  IRubricSettings,
  IRubricVersionSummary,
  RubricCheckType,
  RubricDimension,
  RubricLifecycleAction,
  RubricResultStatus,
  RubricSeverity,
  RubricVersionStatus
} from '../../interfaces/validation/rubric.interface';

// How the rubric screens word what the Validation service returns. One place, so the list, the rubric
// page and the version page say the same thing for the same value.

const STATUS_LABELS: Record<RubricVersionStatus, string> = {
  DRAFT: 'Draft',
  PUBLISHED: 'Published',
  RETIRED: 'Retired'
};

const DIMENSION_LABELS: Record<RubricDimension, string> = {
  CONFORMANCE: 'Conformance',
  TERMINOLOGY: 'Terminology',
  COMPLETENESS: 'Completeness',
  PLAUSIBILITY: 'Plausibility',
  CURRENCY: 'Currency'
};

const CHECK_TYPE_LABELS: Record<RubricCheckType, string> = {
  FHIR_CONFORMANCE: 'FHIR conformance',
  TERMINOLOGY: 'Terminology',
  FHIRPATH: 'FHIRPath',
  VALUESET: 'Value set',
  PLAUSIBILITY: 'Plausibility',
  COMPLETENESS: 'Completeness',
  CURRENCY: 'Currency',
  CUSTOM: 'Custom'
};

const SEVERITY_LABELS: Record<RubricSeverity, string> = {
  ERROR: 'Error',
  WARNING: 'Warning',
  INFORMATION: 'Information'
};

const RESULT_LABELS: Record<RubricResultStatus, string> = {
  ACCEPTABLE: 'Acceptable',
  ACCEPTABLE_WITH_WARNINGS: 'Acceptable with warnings',
  UNACCEPTABLE: 'Unacceptable',
  INCONCLUSIVE: 'Inconclusive'
};

const ACTION_LABELS: Record<RubricLifecycleAction, string> = {
  REGISTERED: 'Saved as draft',
  PUBLISHED: 'Published',
  RETIRED: 'Retired',
  DELETED: 'Draft deleted'
};

const SCORING_LABELS: Record<string, string> = {
  'piqi-dimension-scorecard': 'Dimension scorecard',
  'piqi-check-scorecard': 'Check scorecard',
  'piqi-pass-fail': 'Pass/fail'
};

const ROLLUP_LABELS: Record<string, string> = {
  'worst-of': 'Worst of',
  'best-of': 'Best of',
  'pass-fail': 'Pass/fail',
  'majority': 'Majority',
  'all-must-pass': 'All must pass'
};

/** A value the screens have no wording for is shown as it came, so a new one is visible, not blank. */
function label<T extends string>(labels: Record<T, string>, value: T | null | undefined): string {
  return value ? labels[value] ?? value : '';
}

export const statusLabel = (status?: RubricVersionStatus | null): string => label(STATUS_LABELS, status);
export const dimensionLabel = (dimension?: RubricDimension | null): string => label(DIMENSION_LABELS, dimension);
export const checkTypeLabel = (type?: RubricCheckType | null): string => label(CHECK_TYPE_LABELS, type);
export const severityLabel = (severity?: RubricSeverity | null): string => label(SEVERITY_LABELS, severity);
export const resultLabel = (result?: RubricResultStatus | null): string => label(RESULT_LABELS, result);
export const actionLabel = (action?: RubricLifecycleAction | null): string => label(ACTION_LABELS, action);

/** "Dimension scorecard, worst of", or just the policy when it has no roll-up. */
export function scoringLabel(policy?: IRubricScoringPolicy | null): string {
  if (!policy?.type) {
    return '';
  }
  const type = SCORING_LABELS[policy.type] ?? policy.type;
  if (!policy.rollup) {
    return type;
  }
  return `${type}, ${(ROLLUP_LABELS[policy.rollup] ?? policy.rollup).toLowerCase()}`;
}

/** How the reporting pipeline uses a rubric: its result decides validation, or it is only compared. */
export type RubricPipelineUse = 'decides' | 'compared';

/**
 * Whether the reporting pipeline is using this rubric, and how. A rubric is in use when it is the one the
 * pipeline is set to evaluate against and the rubric engine runs there at all: either deciding whether a
 * patient passes, or running beside the validator that decides so the two can be compared. Named in the
 * settings but with the engine off both ways, it never runs, so it is not in use.
 */
export function pipelineUse(settings: IRubricSettings | null | undefined, rubricId: string): RubricPipelineUse | null {
  if (!settings?.pipelineRubricId || settings.pipelineRubricId !== rubricId) {
    return null;
  }
  if (settings.pipelineEnabled) {
    return 'decides';
  }
  return settings.pipelineShadowEnabled ? 'compared' : null;
}

/** What "In Use" means for this rubric, for a tooltip. */
export function pipelineUseTitle(use: RubricPipelineUse | null): string {
  switch (use) {
    case 'decides':
      return 'The reporting pipeline validates patient data against this rubric\'s newest published version, '
        + 'and its result decides whether the data passes.';
    case 'compared':
      return 'The reporting pipeline runs this rubric\'s newest published version beside the validator that '
        + 'decides, to compare the two. Its result does not decide whether the data passes.';
    default:
      return '';
  }
}

/** The newest version in a status. The service returns a rubric's versions newest first. */
export function newestInStatus(
  versions: IRubricVersionSummary[] | undefined, status: RubricVersionStatus): IRubricVersionSummary | undefined {
  return versions?.find(version => version.status === status);
}

/** "Not run", or the outcome of the last dry run against the version's current definition. */
export function dryRunLabel(version: Pick<IRubricVersionSummary, 'dryRunCompletedAt' | 'dryRunStatus'>): string {
  if (!version.dryRunCompletedAt) {
    return 'Not run';
  }
  return resultLabel(version.dryRunStatus) || 'Completed';
}

/** A check is enabled unless it says otherwise. */
export const isCheckEnabled = (check: IRubricCheck): boolean => check.enabled !== false;

/** A combined value-set check: one that passes or fails on its child checks. */
export const isCombinedCheck = (check: IRubricCheck): boolean => Array.isArray(check.parameters?.checks);

/** One line saying what a check tests, for a table row. */
export function checkSummary(check: IRubricCheck): string {
  const parameters = check.parameters ?? {};

  if (isCombinedCheck(check)) {
    const children = parameters.checks ?? [];
    const mode = parameters.matchMode === 'ALL' ? 'All' : 'Any';
    return `${mode} of ${children.length} ${children.length === 1 ? 'check' : 'checks'}`;
  }

  switch (check.type) {
    case 'FHIR_CONFORMANCE':
      return parameters.profiles?.length
        ? parameters.profiles.join(', ')
        : 'Each resource against its own profile';
    case 'VALUESET':
      return [parameters.path, parameters.valueSet].filter(Boolean).join(' in ');
    case 'TERMINOLOGY':
      return parameters.valueSetWhitelistRegex
        ? `Value sets matching ${parameters.valueSetWhitelistRegex}`
        : 'Codings against terminology';
    case 'CUSTOM':
      return String(parameters['customCheckId'] ?? parameters['className'] ?? '');
    default:
      return parameters.expression ?? '';
  }
}

/** The checks in the order they run: by ordinal, those without one last, otherwise as written. */
export function orderedChecks(definition?: IRubricDefinition | null): IRubricCheck[] {
  const checks = definition?.checks ?? [];
  return checks
    .map((check, index) => ({ check, index }))
    .sort((a, b) =>
      (a.check.ordinal ?? Number.MAX_SAFE_INTEGER) - (b.check.ordinal ?? Number.MAX_SAFE_INTEGER) || a.index - b.index)
    .map(entry => entry.check);
}

/** A setting of a check, worded for a reader. */
export interface CheckSetting {
  key: string;
  label: string;
  value: string;
}

const SETTING_LABELS: Record<string, string> = {
  expression: 'Expression',
  failureMessage: 'Failure message',
  code: 'Code',
  profiles: 'Profiles',
  path: 'Path',
  valueSet: 'Value set',
  system: 'Code system',
  validateCodings: 'Validate codings',
  valueSetWhitelistRegex: 'Value sets matching',
  matchMode: 'Passes when',
  customCheckId: 'Plug-in id',
  className: 'Plug-in class',
  min: 'Minimum',
  max: 'Maximum'
};

/** The name of a check's setting; one the screens have no wording for is shown as it is written. */
export const settingLabel = (key: string): string => SETTING_LABELS[key] ?? key;

/** A setting's value as text. */
export function settingValue(key: string, value: unknown): string {
  if (key === 'matchMode') {
    return value === 'ALL' ? 'All of its checks pass' : 'Any of its checks passes';
  }
  if (typeof value === 'boolean') {
    return value ? 'Yes' : 'No';
  }
  if (Array.isArray(value)) {
    return value.join('\n');
  }
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}

/** A check's settings as label and value, in the order they were written. Child checks are not among them. */
export function checkSettings(check: IRubricCheck): CheckSetting[] {
  return Object.entries(check.parameters ?? {})
    .filter(([key, value]) => key !== 'checks' && value !== null && value !== undefined && value !== '')
    .map(([key, value]) => ({ key, label: settingLabel(key), value: settingValue(key, value) }));
}
