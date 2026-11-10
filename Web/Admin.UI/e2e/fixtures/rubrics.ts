import {
  IRubricApiResponse,
  IRubricDefinition,
  IRubricDetail,
  IRubricLifecycleEvent,
  IRubricPage,
  IRubricRunResult,
  IRubricSettings,
  IRubricSummary,
  IRubricValidationResult,
  IRubricVersionDetail,
  IRubricVersionSummary,
} from '../../src/app/interfaces/validation/rubric.interface';

/**
 * A successful answer from a rubric endpoint, as an api.mock handler.
 *
 * Every rubric endpoint answers with an envelope that has a `status` field of its own. ApiMock reads a
 * handler with a numeric `status` as "fulfil with this status and this `json`", so the envelope cannot be
 * passed to api.mock directly: it has to be the `json` of such a handler.
 */
export function rubricOk<T>(data: T, message = 'OK'): { status: number; json: IRubricApiResponse<T> } {
  return { status: 200, json: { status: 200, message, timestamp: '2026-09-08T10:15:30.184Z', data } };
}

/** The envelope of a refused request, e.g. a rubric that does not exist. */
export function rubricError(status: number, message: string, errors?: string[]): { status: number; json: object } {
  return { status, json: { status, message, timestamp: '2026-09-08T10:15:30.184Z', ...(errors ? { errors } : {}) } };
}

const CHECKSUM_DRAFT = 'b7f1c0a2d94e5f6a8b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d3e2f1a';
const CHECKSUM_PUBLISHED = '1a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f809';
const CHECKSUM_RETIRED = '9f8e7d6c5b4a39281706f5e4d3c2b1a09f8e7d6c5b4a39281706f5e4d3c2b1a0';

/** A rubric with a draft, an active published version and a retired one. */
export const bareMinimumVersions: IRubricVersionSummary[] = [
  {
    rubricVersionId: 12,
    rubricId: 'bare-minimum-requirements',
    semver: '1.2.0',
    status: 'DRAFT',
    checksum: CHECKSUM_DRAFT,
    dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
    scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
    createdAt: '2026-09-06T14:02:11Z',
    createdBy: 'author@example.org',
    checkCount: 5,
  },
  {
    rubricVersionId: 11,
    rubricId: 'bare-minimum-requirements',
    semver: '1.1.0',
    status: 'PUBLISHED',
    checksum: CHECKSUM_PUBLISHED,
    dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
    scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
    createdAt: '2026-09-01T09:00:00Z',
    createdBy: 'author@example.org',
    publishedAt: '2026-09-03T16:30:00Z',
    publishedBy: 'reviewer@example.org',
    dryRunCompletedAt: '2026-09-03T16:10:00Z',
    dryRunStatus: 'ACCEPTABLE_WITH_WARNINGS',
    checkCount: 4,
  },
  {
    rubricVersionId: 10,
    rubricId: 'bare-minimum-requirements',
    semver: '1.0.0',
    status: 'RETIRED',
    checksum: CHECKSUM_RETIRED,
    dimensions: ['CONFORMANCE'],
    scoringPolicy: { type: 'piqi-pass-fail' },
    createdAt: '2026-08-20T09:00:00Z',
    createdBy: 'author@example.org',
    publishedAt: '2026-08-21T09:00:00Z',
    publishedBy: 'reviewer@example.org',
    retiredAt: '2026-09-03T16:31:00Z',
    retiredBy: 'reviewer@example.org',
    checkCount: 1,
  },
];

export const bareMinimumRubric: IRubricDetail = {
  rubricId: 'bare-minimum-requirements',
  title: 'ACH Monthly Bare-Minimum Requirements',
  owner: 'lcg',
  latestPublishedSemver: '1.1.0',
  versions: bareMinimumVersions,
  createdAt: '2026-08-20T09:00:00Z',
  updatedAt: '2026-09-06T14:02:11Z',
};

/** A rubric that only has a draft: nothing published yet. */
export const draftOnlyRubric: IRubricSummary = {
  rubricId: 'cross-measure-patient',
  title: 'Cross-Measure Patient',
  owner: 'lantana',
  versions: [
    {
      rubricVersionId: 20,
      rubricId: 'cross-measure-patient',
      semver: '0.3.0',
      status: 'DRAFT',
      checksum: CHECKSUM_DRAFT,
      createdAt: '2026-09-07T08:00:00Z',
      createdBy: 'author@example.org',
    },
  ],
  createdAt: '2026-09-07T08:00:00Z',
  updatedAt: '2026-09-07T08:00:00Z',
};

/** GET /api/validation/rubrics/$owners */
export const rubricOwners: string[] = ['lantana', 'lcg'];

/** GET /api/validation/rubrics/$settings — the pipeline evaluates against the bare-minimum rubric. */
export const rubricSettings: IRubricSettings = {
  dryRunRequiredForPublish: false,
  pipelineRubricId: 'bare-minimum-requirements',
  pipelineEnabled: false,
  pipelineShadowEnabled: true,
  maxChecks: 200,
  maxPayloadBytes: 262144,
  maxProfiles: 20,
  maxCombinatorChecks: 20,
  maxCombinatorDepth: 5,
  maxApplicableContextEntries: 50,
  checkTypes: [
    { type: 'FHIR_CONFORMANCE', dimension: 'CONFORMANCE', parameters: ['profiles'], supported: true },
    { type: 'TERMINOLOGY', dimension: 'TERMINOLOGY', parameters: ['validateCodings', 'valueSetWhitelistRegex'], supported: true },
    { type: 'FHIRPATH', dimension: null, parameters: ['code', 'expression', 'failureMessage'], supported: true },
    { type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: ['checks', 'code', 'failureMessage', 'matchMode', 'path', 'system', 'valueSet'], supported: true },
    { type: 'PLAUSIBILITY', dimension: 'PLAUSIBILITY', parameters: ['code', 'expression', 'failureMessage'], supported: true },
    { type: 'COMPLETENESS', dimension: 'COMPLETENESS', parameters: ['code', 'expression', 'failureMessage'], supported: true },
    { type: 'CURRENCY', dimension: 'CURRENCY', parameters: ['code', 'expression', 'failureMessage'], supported: true },
    { type: 'CUSTOM', dimension: null, parameters: ['className', 'code', 'customCheckId', 'failureMessage', 'max', 'min', 'path'], supported: false },
  ],
  dimensions: ['CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY', 'CURRENCY'],
  severities: ['ERROR', 'WARNING', 'INFORMATION'],
  scoringPolicies: [
    { type: 'piqi-dimension-scorecard', rollupRequired: true },
    { type: 'piqi-check-scorecard', rollupRequired: true },
    { type: 'piqi-pass-fail', rollupRequired: false },
  ],
  rollups: ['worst-of', 'best-of', 'pass-fail', 'majority', 'all-must-pass'],
  matchModes: ['ALL', 'ANY'],
  fhirResourceTypes: ['DiagnosticReport', 'Encounter', 'Observation', 'Patient'],
};

/** GET /api/validation/rubrics */
export function rubricPage(content: IRubricSummary[], overrides: Partial<IRubricPage<IRubricSummary>> = {}):
  { status: number; json: IRubricApiResponse<IRubricPage<IRubricSummary>> } {
  return rubricOk<IRubricPage<IRubricSummary>>({ content, page: 0, size: 20, totalElements: content.length, totalPages: content.length ? 1 : 0, ...overrides });
}

/** The published version's definition: a FHIRPath check, a single value-set check, a combined one and a disabled one. */
export const publishedDefinition: IRubricDefinition = {
  id: 'bare-minimum-requirements',
  semver: '1.1.0',
  title: 'ACH Monthly Bare-Minimum Requirements',
  owner: 'lcg',
  dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
  applicableContext: { fhirResources: ['DiagnosticReport', 'Patient'], workflowTags: ['nhsn-dqm'] },
  scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
  checks: [
    {
      id: 'diagnosticreport-category-required',
      type: 'FHIRPATH',
      dimension: 'CONFORMANCE',
      severityOverride: 'ERROR',
      ordinal: 1,
      enabled: true,
      parameters: {
        expression: 'DiagnosticReport.category.exists()',
        failureMessage: 'Missing DiagnosticReport.category code.',
        code: 'diagnosticreport-category-required',
      },
    },
    {
      id: 'diagnosticreport-category-terminology',
      type: 'VALUESET',
      dimension: 'TERMINOLOGY',
      severityOverride: 'ERROR',
      ordinal: 2,
      enabled: true,
      parameters: {
        path: 'DiagnosticReport.category',
        valueSet: 'http://hl7.org/fhir/ValueSet/diagnostic-service-sections|4.0.1',
      },
    },
    {
      id: 'specimen-type-terminology',
      type: 'VALUESET',
      dimension: 'TERMINOLOGY',
      severityOverride: 'WARNING',
      ordinal: 3,
      enabled: true,
      parameters: {
        matchMode: 'ANY',
        failureMessage: 'DiagnosticReport.specimen.type should be from ResourceType.',
        checks: [
          { id: 'not-lab', type: 'FHIRPATH', parameters: { expression: 'DiagnosticReport.specimen.empty()' } },
          {
            id: 'valueset',
            type: 'VALUESET',
            parameters: { path: 'DiagnosticReport.specimen.type', valueSet: 'http://hl7.org/fhir/ValueSet/resource-types|4.0.1' },
          },
        ],
      },
    },
    {
      id: 'patient-conformance',
      type: 'FHIR_CONFORMANCE',
      dimension: 'CONFORMANCE',
      severityOverride: 'INFORMATION',
      ordinal: 4,
      enabled: false,
      parameters: {},
    },
  ],
};

/** GET /api/validation/rubrics/bare-minimum-requirements/versions/1.1.0 */
export const publishedVersion: IRubricVersionDetail = {
  rubricVersionId: 11,
  rubricId: 'bare-minimum-requirements',
  semver: '1.1.0',
  status: 'PUBLISHED',
  checksum: CHECKSUM_PUBLISHED,
  definition: publishedDefinition,
  createdAt: '2026-09-01T09:00:00Z',
  createdBy: 'author@example.org',
  publishedAt: '2026-09-03T16:30:00Z',
  publishedBy: 'reviewer@example.org',
  dryRunCompletedAt: '2026-09-03T16:10:00Z',
  dryRunStatus: 'ACCEPTABLE_WITH_WARNINGS',
};

/** GET /api/validation/rubrics/bare-minimum-requirements/events */
export const bareMinimumEvents: IRubricLifecycleEvent[] = [
  { eventId: 'e-4', semver: '1.2.0', action: 'REGISTERED', actor: 'author@example.org', checksum: CHECKSUM_DRAFT, occurredAt: '2026-09-06T14:02:11Z' },
  { eventId: 'e-3', semver: '1.0.0', action: 'RETIRED', actor: 'reviewer@example.org', checksum: CHECKSUM_RETIRED, occurredAt: '2026-09-03T16:31:00Z' },
  { eventId: 'e-2', semver: '1.1.0', action: 'PUBLISHED', actor: 'reviewer@example.org', checksum: CHECKSUM_PUBLISHED, occurredAt: '2026-09-03T16:30:00Z' },
  { eventId: 'e-1', semver: '1.1.0', action: 'REGISTERED', actor: 'author@example.org', checksum: CHECKSUM_PUBLISHED, occurredAt: '2026-09-01T09:00:00Z' },
];

/** The draft version's definition: the published one with a fifth check, under the next version number. */
export const draftDefinition: IRubricDefinition = {
  ...publishedDefinition,
  semver: '1.2.0',
  checks: [
    ...publishedDefinition.checks,
    {
      id: 'patient-name-present',
      type: 'FHIRPATH',
      dimension: 'CONFORMANCE',
      severityOverride: 'WARNING',
      ordinal: 5,
      enabled: true,
      parameters: { expression: 'Patient.name.exists()', failureMessage: 'Patient has no name' },
    },
  ],
};

/** GET /api/validation/rubrics/bare-minimum-requirements/versions/1.2.0 */
export const draftVersion: IRubricVersionDetail = {
  rubricVersionId: 12,
  rubricId: 'bare-minimum-requirements',
  semver: '1.2.0',
  status: 'DRAFT',
  checksum: CHECKSUM_DRAFT,
  definition: draftDefinition,
  createdAt: '2026-09-06T14:02:11Z',
  createdBy: 'author@example.org',
};

/** What saving a draft answers with: the version as saved, with the checksum the next save has to name. */
export function savedDraft(rubricId: string, semver: string, checksum = 'c0ffee'.repeat(10) + 'c0ff'):
  { status: number; json: IRubricApiResponse<IRubricVersionSummary> } {
  return {
    status: 201,
    json: {
      status: 201,
      message: 'Rubric version registered',
      timestamp: '2026-09-08T10:15:30.184Z',
      data: { rubricVersionId: 30, rubricId, semver, status: 'DRAFT', checksum },
    },
  };
}

/** POST /api/validation/rubrics/$validate */
export function validationResult(result: Partial<IRubricValidationResult>):
  { status: number; json: IRubricApiResponse<IRubricValidationResult> } {
  return rubricOk<IRubricValidationResult>({ valid: true, errors: [], ...result });
}

/** What a dry run of the draft finds in a sample: two errors from one check and a warning from another. */
export const dryRunResult: IRubricRunResult = {
  rubricId: 'bare-minimum-requirements',
  rubricVersion: '1.2.0',
  status: 'UNACCEPTABLE',
  score: {
    interpretation: 'UNACCEPTABLE',
    byDimension: { CONFORMANCE: 'UNACCEPTABLE', TERMINOLOGY: 'ACCEPTABLE' },
  },
  summary: { errorCount: 2, warningCount: 1, informationCount: 0 },
  findings: [
    {
      checkId: 'diagnosticreport-category-required', dimension: 'CONFORMANCE', severity: 'ERROR',
      code: 'diagnosticreport-category-required', message: 'Missing DiagnosticReport.category code.',
      location: 'Bundle.entry[2].resource', expression: 'DiagnosticReport.category.exists()',
    },
    {
      checkId: 'diagnosticreport-category-required', dimension: 'CONFORMANCE', severity: 'ERROR',
      code: 'diagnosticreport-category-required', message: 'Missing DiagnosticReport.category code.',
      location: 'Bundle.entry[5].resource', expression: 'DiagnosticReport.category.exists()',
    },
    {
      checkId: 'patient-name-present', dimension: 'CONFORMANCE', severity: 'WARNING',
      message: 'Patient has no name', location: 'Bundle.entry[0].resource', expression: 'Patient.name.exists()',
    },
  ],
  trace: {
    durationMs: 840,
    checkDurationsMs: {
      'diagnosticreport-category-required': 3.2,
      'diagnosticreport-category-terminology': 610.4,
      'specimen-type-terminology': 201.7,
      'patient-name-present': 1.1,
    },
  },
};

/** What publishing or retiring answers with: the version in its new status. */
export function lifecycleResult(semver: string, status: 'PUBLISHED' | 'RETIRED'):
  { status: number; json: IRubricApiResponse<IRubricVersionSummary> } {
  return rubricOk<IRubricVersionSummary>({
    rubricVersionId: 12, rubricId: 'bare-minimum-requirements', semver, status, checksum: CHECKSUM_DRAFT,
  });
}
