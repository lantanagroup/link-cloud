import {
  IRubricCheck,
  IRubricCheckParameters,
  IRubricDefinition,
  IRubricSettings,
  RubricCheckType,
  RubricDimension,
  RubricSeverity
} from '../../interfaces/validation/rubric.interface';

// What the draft editor and the import know about a rubric definition without asking the service: how to
// start one, how to number its next version, what the service will refuse, and how to send it. The service
// has the last word on all of it; this is here so most mistakes are caught while typing, on the field.

// ------------------------------------------------------------------------------------------------
// What the service accepts, for when its settings could not be read. Its own answer is used when there is one.

export const DEFAULT_DIMENSIONS: RubricDimension[] = ['CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY', 'CURRENCY'];
export const DEFAULT_SEVERITIES: RubricSeverity[] = ['ERROR', 'WARNING', 'INFORMATION'];
export const DEFAULT_ROLLUPS = ['worst-of', 'best-of', 'pass-fail', 'majority', 'all-must-pass'];
export const DEFAULT_SCORING_POLICIES = [
  { type: 'piqi-dimension-scorecard', rollupRequired: true },
  { type: 'piqi-check-scorecard', rollupRequired: true },
  { type: 'piqi-pass-fail', rollupRequired: false }
];

/** The dimension a check of each type must use. A type that is absent may use any the rubric declares. */
const DEFAULT_REQUIRED_DIMENSION: Partial<Record<RubricCheckType, RubricDimension>> = {
  FHIR_CONFORMANCE: 'CONFORMANCE',
  TERMINOLOGY: 'TERMINOLOGY',
  VALUESET: 'TERMINOLOGY',
  COMPLETENESS: 'COMPLETENESS',
  PLAUSIBILITY: 'PLAUSIBILITY',
  CURRENCY: 'CURRENCY'
};

const DEFAULT_LIMITS = {
  maxChecks: 200,
  maxProfiles: 20,
  maxCombinatorChecks: 20,
  maxApplicableContextEntries: 50
};

const MAX_ID = 128;
const MAX_TITLE = 256;
const MAX_OWNER = 128;
const MAX_SEMVER = 32;
const MAX_CODE = 128;
const MAX_REGEX = 512;
const MAX_WORKFLOW_TAG = 128;
const RUBRIC_ID_PATTERN = /^[a-z0-9]+([.-][a-z0-9]+)*$/;
const SEMVER_PATTERN = /^\d+\.\d+\.\d+$/;
// An absolute URI, optionally followed by |version: what the service calls a canonical URL.
const CANONICAL_PATTERN = /^[A-Za-z][A-Za-z0-9+.-]*:[^\s|]+(\|\S+)?$/;

/** The check types the form can create. CUSTOM is left out: the engine cannot run one yet. */
export const FORM_CHECK_TYPES: RubricCheckType[] =
  ['FHIRPATH', 'COMPLETENESS', 'PLAUSIBILITY', 'CURRENCY', 'VALUESET', 'FHIR_CONFORMANCE', 'TERMINOLOGY'];

/** The types whose one setting that matters is a FHIRPath expression. */
const EXPRESSION_TYPES: RubricCheckType[] = ['FHIRPATH', 'COMPLETENESS', 'PLAUSIBILITY', 'CURRENCY'];

// ------------------------------------------------------------------------------------------------
// Checks

/** Which form a check's settings take. */
export type RubricCheckKind = 'expression' | 'valueset' | 'combined' | 'conformance' | 'terminology' | 'custom';

export function checkKind(check: Pick<IRubricCheck, 'type' | 'parameters'>): RubricCheckKind {
  if (EXPRESSION_TYPES.includes(check.type)) {
    return 'expression';
  }
  switch (check.type) {
    case 'VALUESET':
      return Array.isArray(check.parameters?.checks) ? 'combined' : 'valueset';
    case 'FHIR_CONFORMANCE':
      return 'conformance';
    case 'TERMINOLOGY':
      return 'terminology';
    default:
      return 'custom';
  }
}

/**
 * Why the form cannot edit a check, or null when it can. A check it cannot edit is shown read-only and
 * sent back exactly as it came, so opening and saving a rubric never loses what the form does not cover.
 */
export function readOnlyReason(check: IRubricCheck): string | null {
  const kind = checkKind(check);
  if (kind === 'custom') {
    return 'Custom checks cannot be edited here.';
  }
  if (kind === 'combined') {
    const children = check.parameters?.checks ?? [];
    if (children.some(child => checkKind(child) === 'combined')) {
      return 'This check combines checks that are themselves combined. Checks nested this deep cannot be edited here.';
    }
    if (children.some(child => checkKind(child) === 'custom')) {
      return 'This check combines a custom check, which cannot be edited here.';
    }
  }
  return null;
}

/** The dimension a check of this type must use, or null when it may use any the rubric declares. */
export function requiredDimension(type: RubricCheckType, settings?: IRubricSettings | null): RubricDimension | null {
  const described = settings?.checkTypes?.find(setting => setting.type === type);
  if (described) {
    return described.dimension ?? null;
  }
  return DEFAULT_REQUIRED_DIMENSION[type] ?? null;
}

/** A check to start from, of a type and with the dimension that type needs. */
export function newCheck(type: RubricCheckType, definition: IRubricDefinition, settings?: IRubricSettings | null): IRubricCheck {
  return {
    id: '',
    type,
    dimension: requiredDimension(type, settings) ?? definition.dimensions[0],
    severityOverride: 'ERROR',
    enabled: true,
    parameters: {}
  };
}

// ------------------------------------------------------------------------------------------------
// Definitions

/** An empty definition for a new rubric. */
export function newDefinition(): IRubricDefinition {
  return {
    id: '',
    semver: '1.0.0',
    title: '',
    owner: '',
    dimensions: ['CONFORMANCE'],
    scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
    checks: []
  };
}

/** A copy that shares nothing with the original, so editing it leaves the original as it was. */
export function cloneDefinition(definition: IRubricDefinition): IRubricDefinition {
  return JSON.parse(JSON.stringify(definition));
}

/**
 * The definition as it is sent to the service: text trimmed, what is empty left out (the service refuses
 * an empty list where it accepts none at all), no roll-up for a policy that has none, and each check
 * numbered by its place in the list, which is the order the checks run in.
 */
export function cleanDefinition(definition: IRubricDefinition): IRubricDefinition {
  const cleaned: IRubricDefinition = {
    id: definition.id.trim(),
    semver: definition.semver.trim(),
    dimensions: [...definition.dimensions],
    scoringPolicy: { type: definition.scoringPolicy.type },
    checks: definition.checks.map((check, index) => cleanCheck(check, index + 1))
  };
  if (definition.title?.trim()) {
    cleaned.title = definition.title.trim();
  }
  if (definition.owner?.trim()) {
    cleaned.owner = definition.owner.trim();
  }
  if (definition.scoringPolicy.rollup && rollupRequired(definition.scoringPolicy.type)) {
    cleaned.scoringPolicy.rollup = definition.scoringPolicy.rollup;
  }

  const resources = trimmedList(definition.applicableContext?.fhirResources);
  const tags = trimmedList(definition.applicableContext?.workflowTags);
  if (resources.length || tags.length) {
    cleaned.applicableContext = {};
    if (resources.length) {
      cleaned.applicableContext.fhirResources = resources;
    }
    if (tags.length) {
      cleaned.applicableContext.workflowTags = tags;
    }
  }
  return cleaned;
}

function cleanCheck(check: IRubricCheck, ordinal: number): IRubricCheck {
  const cleaned: IRubricCheck = {
    id: check.id.trim(),
    type: check.type,
    dimension: check.dimension,
    severityOverride: check.severityOverride,
    ordinal,
    enabled: check.enabled !== false,
    parameters: readOnlyReason(check) ? check.parameters ?? {} : cleanParameters(check)
  };
  return cleaned;
}

/** A check's settings with only the keys its kind uses, trimmed, and without the ones left empty. */
function cleanParameters(check: Pick<IRubricCheck, 'type' | 'parameters'>): IRubricCheckParameters {
  const source = check.parameters ?? {};
  const cleaned: IRubricCheckParameters = {};
  const text = (key: keyof IRubricCheckParameters) => {
    const value = source[key];
    if (typeof value === 'string' && value.trim()) {
      (cleaned as Record<string, unknown>)[key as string] = value.trim();
    }
  };

  switch (checkKind(check)) {
    case 'expression':
      text('expression');
      text('failureMessage');
      text('code');
      break;
    case 'valueset':
      text('path');
      text('valueSet');
      text('system');
      break;
    case 'combined':
      cleaned.matchMode = source.matchMode === 'ALL' ? 'ALL' : 'ANY';
      text('failureMessage');
      text('code');
      cleaned.checks = (source.checks ?? []).map(child => ({
        id: child.id.trim(),
        type: child.type,
        parameters: cleanParameters(child)
      }));
      break;
    case 'conformance': {
      const profiles = trimmedList(source.profiles);
      if (profiles.length) {
        cleaned.profiles = profiles;
      }
      break;
    }
    case 'terminology':
      if (typeof source.validateCodings === 'boolean') {
        cleaned.validateCodings = source.validateCodings;
      }
      text('valueSetWhitelistRegex');
      break;
    default:
      return source;
  }
  return cleaned;
}

function trimmedList(values?: string[]): string[] {
  return (values ?? []).map(value => value.trim()).filter(value => value.length > 0);
}

/** Whether a scoring policy needs a roll-up; the service refuses one for a policy that does not. */
export function rollupRequired(policyType: string, settings?: IRubricSettings | null): boolean {
  const policies = settings?.scoringPolicies?.length ? settings.scoringPolicies : DEFAULT_SCORING_POLICIES;
  return policies.find(policy => policy.type === policyType)?.rollupRequired ?? true;
}

/** Whether two definitions would be stored as the same thing. */
export function sameDefinition(a: IRubricDefinition, b: IRubricDefinition): boolean {
  return JSON.stringify(cleanDefinition(a)) === JSON.stringify(cleanDefinition(b));
}

// ------------------------------------------------------------------------------------------------
// Version numbers

export type VersionPart = 'major' | 'minor' | 'patch';

function parseVersion(semver: string): [number, number, number] | null {
  const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(semver.trim());
  return match ? [Number(match[1]), Number(match[2]), Number(match[3])] : null;
}

/** Negative when a is the older version, positive when it is the newer, zero when they are the same. */
export function compareVersions(a: string, b: string): number {
  const left = parseVersion(a);
  const right = parseVersion(b);
  if (!left || !right) {
    return a.localeCompare(b);
  }
  return left[0] - right[0] || left[1] - right[1] || left[2] - right[2];
}

/** The version after this one, stepping the part asked for and zeroing the parts below it. */
export function bumpVersion(semver: string, part: VersionPart): string {
  const [major, minor, patch] = parseVersion(semver) ?? [0, 0, 0];
  switch (part) {
    case 'major':
      return `${major + 1}.0.0`;
    case 'minor':
      return `${major}.${minor + 1}.0`;
    default:
      return `${major}.${minor}.${patch + 1}`;
  }
}

/**
 * The next version to offer for a rubric: a step above its newest version, whatever that version's state.
 * A step above the version being copied could land on one that already exists.
 */
export function nextVersion(existing: string[], part: VersionPart = 'patch'): string {
  const valid = existing.filter(semver => parseVersion(semver) !== null);
  if (valid.length === 0) {
    return '1.0.0';
  }
  const newest = valid.reduce((a, b) => compareVersions(a, b) >= 0 ? a : b);
  return bumpVersion(newest, part);
}

// ------------------------------------------------------------------------------------------------
// Problems

/** Something wrong with a definition, and where in the form it belongs. */
export interface RubricIssue {
  /** Where the problem is, in the service's own words: `semver`, `checks[c1].parameters.expression`. */
  path: string;
  message: string;
  /** The form field it belongs to; empty when it belongs to the whole section. */
  field: string;
  /** The check it is about, by position; absent when it is about the rubric itself. */
  checkIndex?: number;
}

function issue(path: string, message: string, field: string, checkIndex?: number): RubricIssue {
  return checkIndex === undefined ? { path, message, field } : { path, message, field, checkIndex };
}

/** Problems for one field of the rubric, or of one check. */
export function issuesFor(issues: RubricIssue[], field: string, checkIndex?: number): RubricIssue[] {
  return issues.filter(candidate => candidate.field === field && candidate.checkIndex === checkIndex);
}

/** Every problem with one check, whichever field it is on. */
export function issuesForCheck(issues: RubricIssue[], checkIndex: number): RubricIssue[] {
  return issues.filter(candidate => candidate.checkIndex === checkIndex);
}

/**
 * What the service would refuse about a definition, as far as that can be told without asking it. It
 * cannot tell whether a FHIRPath expression parses, for one; Validate asks the service for the rest.
 */
export function findIssues(definition: IRubricDefinition, settings?: IRubricSettings | null): RubricIssue[] {
  const issues: RubricIssue[] = [];
  const limits = { ...DEFAULT_LIMITS, ...(settings ?? {}) };

  const id = definition.id.trim();
  if (!id) {
    issues.push(issue('id', 'Enter an id for the rubric.', 'id'));
  } else if (id.length < 3 || id.length > MAX_ID) {
    issues.push(issue('id', `The id must be 3 to ${MAX_ID} characters.`, 'id'));
  } else if (!RUBRIC_ID_PATTERN.test(id)) {
    issues.push(issue('id',
      'Use lower-case letters and digits, in parts separated by a dot or a hyphen, such as piqi.core or bundle-checks-v1.', 'id'));
  }

  const semver = definition.semver.trim();
  if (!semver) {
    issues.push(issue('semver', 'Enter a version.', 'semver'));
  } else if (!SEMVER_PATTERN.test(semver) || semver.length > MAX_SEMVER) {
    issues.push(issue('semver', 'The version must be three numbers separated by dots, such as 1.2.0.', 'semver'));
  } else if (compareVersions(semver, '0.0.0') === 0) {
    issues.push(issue('semver', '0.0.0 cannot be used as a version.', 'semver'));
  }

  if ((definition.title?.trim().length ?? 0) > MAX_TITLE) {
    issues.push(issue('title', `The title can be at most ${MAX_TITLE} characters.`, 'title'));
  }
  if ((definition.owner?.trim().length ?? 0) > MAX_OWNER) {
    issues.push(issue('owner', `The owner can be at most ${MAX_OWNER} characters.`, 'owner'));
  }

  if (definition.dimensions.length === 0) {
    issues.push(issue('dimensions', 'Choose at least one dimension.', 'dimensions'));
  }

  if (!definition.scoringPolicy?.type) {
    issues.push(issue('scoringPolicy.type', 'Choose how the rubric is scored.', 'scoringPolicy.type'));
  } else if (rollupRequired(definition.scoringPolicy.type, settings) && !definition.scoringPolicy.rollup) {
    issues.push(issue('scoringPolicy.rollup', 'Choose a roll-up.', 'scoringPolicy.rollup'));
  }

  findListIssues(issues, 'applicableContext.fhirResources', definition.applicableContext?.fhirResources,
    limits.maxApplicableContextEntries, 'resource type',
    value => settings?.fhirResourceTypes?.length && !settings.fhirResourceTypes.includes(value)
      ? `"${value}" is not a FHIR R4 resource type.` : null);
  findListIssues(issues, 'applicableContext.workflowTags', definition.applicableContext?.workflowTags,
    limits.maxApplicableContextEntries, 'workflow tag',
    value => value.length > MAX_WORKFLOW_TAG ? `A workflow tag can be at most ${MAX_WORKFLOW_TAG} characters.` : null);

  if (definition.checks.length === 0) {
    issues.push(issue('checks', 'Add at least one check.', 'checks'));
  } else {
    if (definition.checks.length > limits.maxChecks) {
      issues.push(issue('checks', `A rubric can have at most ${limits.maxChecks} checks; this one has ${definition.checks.length}.`, 'checks'));
    }
    if (definition.checks.every(check => check.enabled === false)) {
      issues.push(issue('checks', 'At least one check must be enabled.', 'checks'));
    }
  }

  const seenIds = new Map<string, number>();
  definition.checks.forEach((check, index) => {
    findCheckIssues(issues, check, index, definition, settings, limits);
    const checkId = check.id.trim();
    if (checkId) {
      if (seenIds.has(checkId)) {
        issues.push(issue(`checks[${checkId}]`, `Another check already has the id "${checkId}".`, 'id', index));
      } else {
        seenIds.set(checkId, index);
      }
    }
  });

  return issues;
}

function findListIssues(
  issues: RubricIssue[], path: string, values: string[] | undefined, max: number, noun: string,
  each: (value: string) => string | null | undefined | false | 0 | ''): void {
  const list = trimmedList(values);
  if (list.length > max) {
    issues.push(issue(path, `At most ${max} ${noun}s can be listed.`, path));
  }
  const seen = new Set<string>();
  for (const value of list) {
    if (seen.has(value)) {
      issues.push(issue(path, `"${value}" is listed more than once.`, path));
    }
    seen.add(value);
    const problem = each(value);
    if (problem) {
      issues.push(issue(path, problem, path));
    }
  }
}

function findCheckIssues(
  issues: RubricIssue[], check: IRubricCheck, index: number, definition: IRubricDefinition,
  settings: IRubricSettings | null | undefined, limits: typeof DEFAULT_LIMITS): void {
  // A check the form cannot edit is the service's to judge: it cannot be put right here.
  if (readOnlyReason(check)) {
    return;
  }
  const label = check.id.trim() || `#${index + 1}`;
  const at = (suffix: string) => `checks[${label}]${suffix}`;
  const add = (field: string, message: string) => issues.push(issue(at(field ? `.${field}` : ''), message, field, index));

  if (!check.id.trim()) {
    add('id', 'Enter an id for the check.');
  } else if (check.id.trim().length > MAX_ID) {
    add('id', `The id can be at most ${MAX_ID} characters.`);
  }
  if (!check.severityOverride) {
    add('severityOverride', 'Choose a severity.');
  }
  if (!check.dimension) {
    add('dimension', 'Choose a dimension.');
  } else {
    const required = requiredDimension(check.type, settings);
    if (required && check.dimension !== required) {
      add('dimension', `A check of this type must use the ${required.toLowerCase()} dimension.`);
    } else if (!definition.dimensions.includes(check.dimension)) {
      add('dimension', `The rubric does not include the ${check.dimension.toLowerCase()} dimension. Add it on the Details tab, or choose another.`);
    }
  }

  findParameterIssues(check, limits, add, '');
}

function findParameterIssues(
  check: Pick<IRubricCheck, 'type' | 'parameters'>, limits: typeof DEFAULT_LIMITS,
  add: (field: string, message: string) => void, prefix: string): void {
  const parameters = check.parameters ?? {};
  const field = (name: string) => `${prefix}parameters.${name}`;
  const text = (name: keyof IRubricCheckParameters) => {
    const value = parameters[name];
    return typeof value === 'string' ? value.trim() : '';
  };
  const code = () => {
    if (text('code').length > MAX_CODE) {
      add(field('code'), `The code can be at most ${MAX_CODE} characters.`);
    }
  };

  switch (checkKind(check)) {
    case 'expression':
      if (!text('expression')) {
        add(field('expression'), 'Enter a FHIRPath expression.');
      }
      code();
      break;

    case 'valueset':
      if (!text('path')) {
        add(field('path'), 'Enter the path to the coded element.');
      }
      if (!text('valueSet')) {
        add(field('valueSet'), 'Enter the value set.');
      } else if (!CANONICAL_PATTERN.test(text('valueSet'))) {
        add(field('valueSet'), 'The value set must be a full URL, optionally followed by |version.');
      }
      if (text('system') && !CANONICAL_PATTERN.test(text('system'))) {
        add(field('system'), 'The code system must be a full URL, optionally followed by |version.');
      }
      break;

    case 'combined': {
      const children = parameters.checks ?? [];
      if (children.length === 0) {
        add(field('checks'), 'Add at least one check to combine.');
      } else if (children.length > limits.maxCombinatorChecks) {
        add(field('checks'), `At most ${limits.maxCombinatorChecks} checks can be combined.`);
      }
      code();
      const seen = new Set<string>();
      children.forEach((child, childIndex) => {
        const childPrefix = `${prefix}parameters.checks[${childIndex}].`;
        const childId = child.id.trim();
        if (!childId) {
          add(`${childPrefix}id`, 'Enter an id for this check.');
        } else if (childId.length > MAX_ID) {
          add(`${childPrefix}id`, `The id can be at most ${MAX_ID} characters.`);
        } else if (seen.has(childId)) {
          add(`${childPrefix}id`, `Another check in this group already has the id "${childId}".`);
        }
        seen.add(childId);
        findParameterIssues(child, limits, add, childPrefix);
      });
      break;
    }

    case 'conformance': {
      const profiles = trimmedList(parameters.profiles);
      if (profiles.length > limits.maxProfiles) {
        add(field('profiles'), `At most ${limits.maxProfiles} profiles can be listed.`);
      }
      const seen = new Set<string>();
      for (const profile of profiles) {
        if (!CANONICAL_PATTERN.test(profile)) {
          add(field('profiles'), `"${profile}" is not a full URL.`);
        } else if (seen.has(profile)) {
          add(field('profiles'), `"${profile}" is listed more than once.`);
        }
        seen.add(profile);
      }
      break;
    }

    case 'terminology': {
      const pattern = text('valueSetWhitelistRegex');
      if (pattern.length > MAX_REGEX) {
        add(field('valueSetWhitelistRegex'), `The pattern can be at most ${MAX_REGEX} characters.`);
      } else if (pattern) {
        try {
          new RegExp(pattern);
        } catch {
          add(field('valueSetWhitelistRegex'), 'This is not a valid regular expression.');
        }
      }
      break;
    }
  }
}

// ------------------------------------------------------------------------------------------------
// The service's own problems

const DETAIL_FIELDS = ['id', 'semver', 'title', 'owner', 'dimensions'];

/**
 * Places one of the service's errors on the form. The service words each as `<where>: <what>`, with a
 * check named by its id (`checks[c1]`), by its position when it has none (`checks[0]`), or, for a child
 * of a combined check, by `parent.child`. An error that names no place the form knows is kept whole,
 * for the summary.
 */
export function placeServerError(error: string, definition: IRubricDefinition): RubricIssue {
  const separator = error.indexOf(': ');
  const path = separator > 0 ? error.slice(0, separator) : '';
  const message = separator > 0 ? error.slice(separator + 2) : error;

  const check = /^checks\[([^\]]*)\](.*)$/.exec(path);
  if (!check) {
    if (DETAIL_FIELDS.includes(path) || path === 'checks'
      || path.startsWith('scoringPolicy') || path.startsWith('applicableContext')) {
      // `applicableContext.fhirResources[0]` belongs to the list it is an entry of.
      return issue(path, message, path.replace(/\[\d+\]$/, '').replace(/^scoringPolicy$/, 'scoringPolicy.type'));
    }
    return issue(path, message, '');
  }

  const [, label, rest] = check;
  const checkIndex = findCheck(definition, label);
  if (checkIndex === undefined) {
    return issue(path, message, '');
  }
  return issue(path, message, serverCheckField(rest, message), checkIndex);
}

function findCheck(definition: IRubricDefinition, label: string): number | undefined {
  const byId = definition.checks.findIndex(check => check.id === label);
  if (byId >= 0) {
    return byId;
  }
  // A child of a combined check is named parent.child; it belongs with its parent.
  const byParent = definition.checks.findIndex(check => check.id && label.startsWith(`${check.id}.`));
  if (byParent >= 0) {
    return byParent;
  }
  if (/^\d+$/.test(label)) {
    const position = Number(label);
    return position < definition.checks.length ? position : undefined;
  }
  return undefined;
}

/** The field of a check an error is about: named after the `]`, or in the message when it is not. */
function serverCheckField(rest: string, message: string): string {
  const named = rest.replace(/^\./, '').replace(/\[\d+\]$/, '');
  if (named && named !== 'parameters') {
    return named;
  }
  const required = /requires parameters\.(\w+)/.exec(message);
  if (required) {
    return `parameters.${required[1]}`;
  }
  if (/FHIRPath expression/i.test(message)) {
    return 'parameters.expression';
  }
  const leading = /^(id|type|dimension|severityOverride|ordinal)\b/.exec(message);
  if (leading) {
    return leading[1];
  }
  if (/duplicate check id/.test(message)) {
    return 'id';
  }
  return '';
}
