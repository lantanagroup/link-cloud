import {
  IRubricCheck,
  IRubricDefinition,
  IRubricSettings,
  IRubricVersionSummary
} from '../../interfaces/validation/rubric.interface';
import {
  actionLabel,
  checkSummary,
  dryRunLabel,
  isCheckEnabled,
  isCombinedCheck,
  newestInStatus,
  orderedChecks,
  pipelineUse,
  pipelineUseTitle,
  scoringLabel,
  statusLabel
} from './rubric-display';

function version(semver: string, status: IRubricVersionSummary['status']): IRubricVersionSummary {
  return { rubricVersionId: 1, rubricId: 'r', semver, status, checksum: 'c' };
}

function check(overrides: Partial<IRubricCheck>): IRubricCheck {
  return { id: 'c', type: 'FHIRPATH', ...overrides };
}

describe('rubric display', () => {
  describe('labels', () => {
    it('words a known value', () => {
      expect(statusLabel('PUBLISHED')).toBe('Published');
    });

    it('words a deleted draft in the history', () => {
      expect(actionLabel('DELETED')).toBe('Draft deleted');
      expect(actionLabel('REGISTERED')).toBe('Saved as draft');
    });

    it('shows a value it has no wording for as it came, and nothing for no value', () => {
      expect(statusLabel('ARCHIVED' as never)).toBe('ARCHIVED');
      expect(statusLabel(undefined)).toBe('');
    });
  });

  describe('pipelineUse', () => {
    const settings = (enabled: boolean, shadow: boolean) =>
      ({ pipelineRubricId: 'piqi.core', pipelineEnabled: enabled, pipelineShadowEnabled: shadow }) as IRubricSettings;

    it('says the pipeline\'s rubric decides when the rubric engine decides, whatever the comparison is set to', () => {
      expect(pipelineUse(settings(true, false), 'piqi.core')).toBe('decides');
      expect(pipelineUse(settings(true, true), 'piqi.core')).toBe('decides');
    });

    it('says it is compared when the engine only runs beside the validator that decides', () => {
      expect(pipelineUse(settings(false, true), 'piqi.core')).toBe('compared');
    });

    it('is null when the engine does not run in the pipeline, for any other rubric, and without settings', () => {
      expect(pipelineUse(settings(false, false), 'piqi.core')).toBeNull();
      expect(pipelineUse(settings(true, true), 'other')).toBeNull();
      expect(pipelineUse(null, 'piqi.core')).toBeNull();
      expect(pipelineUse({ pipelineEnabled: true } as IRubricSettings, 'piqi.core')).toBeNull();
    });

    it('has a tooltip for each use and none for no use', () => {
      expect(pipelineUseTitle('decides')).toContain('decides whether the data passes');
      expect(pipelineUseTitle('compared')).toContain('does not decide');
      expect(pipelineUseTitle(null)).toBe('');
    });
  });

  describe('scoringLabel', () => {
    it('joins the policy and its roll-up', () => {
      expect(scoringLabel({ type: 'piqi-dimension-scorecard', rollup: 'worst-of' })).toBe('Dimension scorecard, worst of');
    });

    it('is the policy alone when there is no roll-up', () => {
      expect(scoringLabel({ type: 'piqi-pass-fail' })).toBe('Pass/fail');
    });

    it('shows slugs it does not know as they came', () => {
      expect(scoringLabel({ type: 'new-policy', rollup: 'new-rollup' })).toBe('new-policy, new-rollup');
    });

    it('is empty without a policy', () => {
      expect(scoringLabel(undefined)).toBe('');
    });
  });

  describe('newestInStatus', () => {
    // The service returns versions newest first.
    const versions = [version('2.0.0', 'DRAFT'), version('1.1.0', 'PUBLISHED'), version('1.0.0', 'PUBLISHED')];

    it('is the first version in that status', () => {
      expect(newestInStatus(versions, 'PUBLISHED')?.semver).toBe('1.1.0');
      expect(newestInStatus(versions, 'DRAFT')?.semver).toBe('2.0.0');
    });

    it('is undefined when none is in that status, or there are no versions', () => {
      expect(newestInStatus(versions, 'RETIRED')).toBeUndefined();
      expect(newestInStatus(undefined, 'DRAFT')).toBeUndefined();
    });
  });

  describe('dryRunLabel', () => {
    it('says a version was never dry-run', () => {
      expect(dryRunLabel({})).toBe('Not run');
    });

    it('gives the outcome of a completed dry run', () => {
      expect(dryRunLabel({ dryRunCompletedAt: '2026-09-03T16:10:00Z', dryRunStatus: 'UNACCEPTABLE' })).toBe('Unacceptable');
    });

    it('says completed when a dry run finished without an outcome', () => {
      expect(dryRunLabel({ dryRunCompletedAt: '2026-09-03T16:10:00Z' })).toBe('Completed');
    });
  });

  describe('isCheckEnabled', () => {
    it('is true unless the check says otherwise', () => {
      expect(isCheckEnabled(check({}))).toBeTrue();
      expect(isCheckEnabled(check({ enabled: true }))).toBeTrue();
      expect(isCheckEnabled(check({ enabled: false }))).toBeFalse();
    });
  });

  describe('checkSummary', () => {
    it('is the expression of a FHIRPath-style check', () => {
      expect(checkSummary(check({ type: 'COMPLETENESS', parameters: { expression: 'Patient.name.exists()' } })))
        .toBe('Patient.name.exists()');
    });

    it('is the path and value set of a single value-set check', () => {
      expect(checkSummary(check({ type: 'VALUESET', parameters: { path: 'Patient.gender', valueSet: 'http://vs' } })))
        .toBe('Patient.gender in http://vs');
    });

    it('counts the children of a combined value-set check', () => {
      const combined = check({
        type: 'VALUESET',
        parameters: { matchMode: 'ALL', checks: [check({ id: 'a' }), check({ id: 'b' })] }
      });
      expect(isCombinedCheck(combined)).toBeTrue();
      expect(checkSummary(combined)).toBe('All of 2 checks');
    });

    it('treats a combined check without a match mode as any, and counts one child in the singular', () => {
      expect(checkSummary(check({ type: 'VALUESET', parameters: { checks: [check({ id: 'a' })] } }))).toBe('Any of 1 check');
    });

    it('lists the profiles of a conformance check, or says each resource is checked against its own', () => {
      expect(checkSummary(check({ type: 'FHIR_CONFORMANCE', parameters: { profiles: ['http://a', 'http://b'] } })))
        .toBe('http://a, http://b');
      expect(checkSummary(check({ type: 'FHIR_CONFORMANCE', parameters: {} }))).toBe('Each resource against its own profile');
      expect(checkSummary(check({ type: 'FHIR_CONFORMANCE' }))).toBe('Each resource against its own profile');
    });

    it('is empty for a check with nothing to summarise', () => {
      expect(checkSummary(check({ type: 'FHIRPATH' }))).toBe('');
    });
  });

  describe('orderedChecks', () => {
    function definition(checks: IRubricCheck[]): IRubricDefinition {
      return { id: 'r', semver: '1.0.0', dimensions: ['CONFORMANCE'], scoringPolicy: { type: 'piqi-pass-fail' }, checks };
    }

    it('orders by ordinal, with checks that have none last in the order written', () => {
      const ordered = orderedChecks(definition([
        check({ id: 'no-ordinal-1' }),
        check({ id: 'third', ordinal: 3 }),
        check({ id: 'no-ordinal-2' }),
        check({ id: 'first', ordinal: 0 })
      ]));
      expect(ordered.map(c => c.id)).toEqual(['first', 'third', 'no-ordinal-1', 'no-ordinal-2']);
    });

    it('does not reorder the definition it was given', () => {
      const source = definition([check({ id: 'b', ordinal: 2 }), check({ id: 'a', ordinal: 1 })]);
      orderedChecks(source);
      expect(source.checks.map(c => c.id)).toEqual(['b', 'a']);
    });

    it('is empty without a definition', () => {
      expect(orderedChecks(undefined)).toEqual([]);
    });
  });
});
