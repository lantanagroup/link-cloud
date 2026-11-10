// The Validation service's rubric registry (api/validation/rubrics), as it serializes it.

export type RubricVersionStatus = 'DRAFT' | 'PUBLISHED' | 'RETIRED';
export type RubricResultStatus = 'ACCEPTABLE' | 'ACCEPTABLE_WITH_WARNINGS' | 'UNACCEPTABLE' | 'INCONCLUSIVE';
export type RubricDimension = 'CONFORMANCE' | 'TERMINOLOGY' | 'COMPLETENESS' | 'PLAUSIBILITY' | 'CURRENCY';
export type RubricCheckType =
  'FHIR_CONFORMANCE' | 'TERMINOLOGY' | 'FHIRPATH' | 'VALUESET' | 'PLAUSIBILITY' | 'COMPLETENESS' | 'CURRENCY' | 'CUSTOM';
export type RubricSeverity = 'ERROR' | 'WARNING' | 'INFORMATION';
export type RubricLifecycleAction = 'REGISTERED' | 'PUBLISHED' | 'RETIRED' | 'DELETED';

/** The envelope every rubric endpoint answers with. `data` is absent on an error, `errors` on a success. */
export interface IRubricApiResponse<T> {
  status: number;
  message: string;
  timestamp: string;
  data?: T;
  errors?: string[];
}

/** One page of a list. `page` is zero-based. */
export interface IRubricPage<T> {
  content: T[];
  page: number;
  size: number;
  totalElements: number;
  totalPages: number;
}

export interface IRubricScoringPolicy {
  /** A slug such as `piqi-dimension-scorecard`. */
  type: string;
  /** A slug such as `worst-of`. Absent for `piqi-pass-fail`. */
  rollup?: string;
}

export interface IRubricApplicableContext {
  fhirResources?: string[];
  workflowTags?: string[];
}

export interface IRubricVersionSummary {
  rubricVersionId: number;
  rubricId: string;
  semver: string;
  status: RubricVersionStatus;
  checksum: string;
  dimensions?: RubricDimension[];
  applicableContext?: IRubricApplicableContext;
  scoringPolicy?: IRubricScoringPolicy;
  createdAt?: string;
  createdBy?: string;
  publishedAt?: string;
  publishedBy?: string;
  retiredAt?: string;
  retiredBy?: string;
  dryRunCompletedAt?: string;
  dryRunStatus?: RubricResultStatus;
  /** How many checks the version has. Present where versions are listed. */
  checkCount?: number;
}

/** A rubric in the list. Its versions are newest first. */
export interface IRubricSummary {
  rubricId: string;
  title?: string;
  owner?: string;
  versions?: IRubricVersionSummary[];
  createdAt?: string;
  updatedAt?: string;
}

export interface IRubricDetail extends IRubricSummary {
  /** The version validation uses: the newest published one. Absent when none is published. */
  latestPublishedSemver?: string;
}

export interface IRubricCheck {
  id: string;
  type: RubricCheckType;
  /** Absent on the child of a combined value-set check, which takes its parent's. */
  dimension?: RubricDimension;
  severityOverride?: RubricSeverity;
  ordinal?: number;
  /** Absent means enabled. */
  enabled?: boolean;
  parameters?: IRubricCheckParameters;
}

/** The settings of a check. Which keys are present depends on the check's type. */
export interface IRubricCheckParameters {
  // FHIR_CONFORMANCE
  profiles?: string[];
  // FHIRPATH, COMPLETENESS, PLAUSIBILITY, CURRENCY
  expression?: string;
  // TERMINOLOGY
  validateCodings?: boolean;
  valueSetWhitelistRegex?: string;
  // VALUESET, single
  path?: string;
  valueSet?: string;
  system?: string;
  // VALUESET, combined
  matchMode?: 'ANY' | 'ALL';
  checks?: IRubricCheck[];
  // shared
  failureMessage?: string;
  code?: string;
  [key: string]: unknown;
}

/** What a version was registered with, and what registering a version accepts. */
export interface IRubricDefinition {
  id: string;
  semver: string;
  title?: string;
  owner?: string;
  dimensions: RubricDimension[];
  applicableContext?: IRubricApplicableContext;
  scoringPolicy: IRubricScoringPolicy;
  checks: IRubricCheck[];
}

export interface IRubricVersionDetail {
  rubricVersionId: number;
  rubricId: string;
  semver: string;
  status: RubricVersionStatus;
  checksum: string;
  definition?: IRubricDefinition;
  createdAt?: string;
  createdBy?: string;
  publishedAt?: string;
  publishedBy?: string;
  retiredAt?: string;
  retiredBy?: string;
  dryRunCompletedAt?: string;
  dryRunStatus?: RubricResultStatus;
}

export interface IRubricLifecycleEvent {
  eventId: string;
  semver: string;
  action: RubricLifecycleAction;
  actor?: string;
  checksum?: string;
  occurredAt: string;
}

/** A check type as the service describes it. */
export interface IRubricCheckTypeSetting {
  type: RubricCheckType;
  /** The dimension a check of this type must use; absent when it may use any the rubric declares. */
  dimension?: RubricDimension | null;
  /** The parameter keys a check of this type accepts. */
  parameters: string[];
  /** False for a type the engine defines but cannot run yet. */
  supported: boolean;
}

export interface IRubricScoringPolicySetting {
  type: string;
  /** True when a roll-up must be given; false when it must be absent. */
  rollupRequired: boolean;
}

/** How the service treats rubrics: its limits, the values it accepts, and what changes what an action does. */
export interface IRubricSettings {
  dryRunRequiredForPublish: boolean;
  /** The rubric the reporting pipeline evaluates against. */
  pipelineRubricId?: string;
  pipelineEnabled: boolean;
  pipelineShadowEnabled: boolean;
  maxChecks: number;
  maxPayloadBytes: number;
  maxProfiles: number;
  maxCombinatorChecks: number;
  maxCombinatorDepth: number;
  maxApplicableContextEntries: number;
  checkTypes: IRubricCheckTypeSetting[];
  dimensions: RubricDimension[];
  severities: RubricSeverity[];
  scoringPolicies: IRubricScoringPolicySetting[];
  rollups: string[];
  matchModes: string[];
  fhirResourceTypes: string[];
}

export type RubricDefinitionFormat = 'json' | 'yaml';

/** What checking a definition found, without saving it. */
export interface IRubricValidationResult {
  /** Whether saving this definition would be accepted. */
  valid: boolean;
  /** Every problem, each as `<where>: <what>`. Empty when valid. */
  errors: string[];
  /** The definition as JSON. Present whenever the text could be read, valid or not. */
  definition?: IRubricDefinition;
  /** The checksum it would be stored with. Only when valid. */
  checksum?: string;
  rubricExists?: boolean;
  /** The status of the version already saved under this id and version number, when there is one. */
  existingStatus?: RubricVersionStatus;
  existingChecksum?: string;
}

/**
 * What the caller says a version's state is when saving it, so a save made on stale knowledge is refused
 * instead of replacing someone else's work. With neither, a draft is replaced whatever it holds.
 */
export interface IRubricSavePrecondition {
  /** The checksum of the draft that was opened. Refused when the draft has since changed or been deleted. */
  expectedChecksum?: string;
  /** The version is being created. Refused when it already exists. */
  mustNotExist?: boolean;
}

/** One thing a run of a rubric found in the sample. */
export interface IRubricFinding {
  checkId: string;
  dimension?: RubricDimension;
  /** The severity that counts: a category's override when one applied, the check's own otherwise. */
  severity: RubricSeverity;
  code?: string;
  message?: string;
  /** Where in the sample, such as `Bundle.entry[3].resource`. */
  location?: string;
  expression?: string;
  /** The severity the check gave, before any override. */
  originalSeverity?: RubricSeverity;
  /** Present only when a category changed the severity. */
  overriddenSeverity?: RubricSeverity;
}

/** What running a version against a sample found. A dry run saves none of it but the outcome. */
export interface IRubricRunResult {
  rubricId: string;
  rubricVersion: string;
  status: RubricResultStatus;
  score?: {
    interpretation?: RubricResultStatus;
    /** The outcome of each dimension, under the dimension scorecard. */
    byDimension?: Partial<Record<RubricDimension, RubricResultStatus>>;
    /** The outcome of each check, under the check scorecard. */
    byCheck?: Record<string, RubricResultStatus>;
  };
  summary?: {
    errorCount: number;
    warningCount: number;
    informationCount: number;
  };
  findings?: IRubricFinding[];
  trace?: {
    durationMs?: number;
    /** How long each enabled check took, by check id, in the order they ran. */
    checkDurationsMs?: Record<string, number>;
  };
}

/** What the rubric list can be asked for. */
export interface IRubricListQuery {
  /** Only rubrics with a version in this status. */
  status?: RubricVersionStatus;
  /** Text to find in the rubric's id, title or owner. */
  search?: string;
  /** Only rubrics with exactly this owner. */
  owner?: string;
  /** With a status: return every version of the rubrics it matched, not only the versions in that status. */
  allVersions?: boolean;
  /** Zero-based. */
  page?: number;
  size?: number;
  /** A field of the rubric: `rubricId`, `title`, `owner` or `updatedAt`. */
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}
