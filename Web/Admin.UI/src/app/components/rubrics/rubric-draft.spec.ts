import { IRubricCheck, IRubricDefinition, IRubricSettings } from '../../interfaces/validation/rubric.interface';
import {
  bumpVersion,
  checkKind,
  cleanDefinition,
  cloneDefinition,
  compareVersions,
  findIssues,
  issuesFor,
  issuesForCheck,
  newCheck,
  newDefinition,
  nextVersion,
  placeServerError,
  readOnlyReason,
  requiredDimension,
  rollupRequired,
  sameDefinition
} from './rubric-draft';

function check(overrides: Partial<IRubricCheck> = {}): IRubricCheck {
  return {
    id: 'c1', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', enabled: true,
    parameters: { expression: 'Patient.name.exists()' }, ...overrides
  };
}

function definition(overrides: Partial<IRubricDefinition> = {}): IRubricDefinition {
  return {
    id: 'piqi.core', semver: '1.0.0', title: 'PIQI Core', owner: 'lcg',
    dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
    scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
    checks: [check()], ...overrides
  };
}

const fields = (d: IRubricDefinition, settings?: IRubricSettings | null) =>
  findIssues(d, settings).map(issue => `${issue.checkIndex ?? ''}|${issue.field}`);

describe('rubric draft', () => {
  describe('checkKind and readOnlyReason', () => {
    it('tells the kinds of check apart', () => {
      expect(checkKind(check())).toBe('expression');
      expect(checkKind(check({ type: 'COMPLETENESS' }))).toBe('expression');
      expect(checkKind(check({ type: 'VALUESET', parameters: { path: 'p', valueSet: 'v' } }))).toBe('valueset');
      expect(checkKind(check({ type: 'VALUESET', parameters: { checks: [] } }))).toBe('combined');
      expect(checkKind(check({ type: 'FHIR_CONFORMANCE' }))).toBe('conformance');
      expect(checkKind(check({ type: 'TERMINOLOGY' }))).toBe('terminology');
      expect(checkKind(check({ type: 'CUSTOM' }))).toBe('custom');
    });

    it('lets the form edit ordinary checks and a combined check one level deep', () => {
      expect(readOnlyReason(check())).toBeNull();
      expect(readOnlyReason(check({
        type: 'VALUESET', parameters: { checks: [{ id: 'a', type: 'FHIRPATH', parameters: { expression: 'true' } }] }
      }))).toBeNull();
    });

    it('does not let the form edit a custom check, or a combined check that nests another or holds a custom one', () => {
      expect(readOnlyReason(check({ type: 'CUSTOM' }))).toContain('Custom checks');
      expect(readOnlyReason(check({
        type: 'VALUESET', parameters: { checks: [{ id: 'a', type: 'VALUESET', parameters: { checks: [] } }] }
      }))).toContain('nested this deep');
      expect(readOnlyReason(check({
        type: 'VALUESET', parameters: { checks: [{ id: 'a', type: 'CUSTOM', parameters: {} }] }
      }))).toContain('custom check');
    });
  });

  describe('requiredDimension and newCheck', () => {
    it('pins the self-named types and leaves FHIRPath free', () => {
      expect(requiredDimension('VALUESET')).toBe('TERMINOLOGY');
      expect(requiredDimension('COMPLETENESS')).toBe('COMPLETENESS');
      expect(requiredDimension('FHIRPATH')).toBeNull();
    });

    it('uses the service\'s answer when it has one', () => {
      const settings = { checkTypes: [{ type: 'FHIRPATH', dimension: 'CURRENCY', parameters: [], supported: true }] } as unknown as IRubricSettings;
      expect(requiredDimension('FHIRPATH', settings)).toBe('CURRENCY');
      expect(requiredDimension('VALUESET', settings)).toBe('TERMINOLOGY'); // not described: the built-in rule
    });

    it('starts a check with the dimension its type needs, or the rubric\'s first when the type is free', () => {
      const draft = definition({ dimensions: ['TERMINOLOGY', 'CONFORMANCE'] });
      expect(newCheck('COMPLETENESS', draft).dimension).toBe('COMPLETENESS');
      expect(newCheck('FHIRPATH', draft).dimension).toBe('TERMINOLOGY');
      expect(newCheck('FHIRPATH', draft)).toEqual(jasmine.objectContaining({ id: '', severityOverride: 'ERROR', enabled: true }));
    });
  });

  describe('cleanDefinition', () => {
    it('trims text and leaves out what is empty', () => {
      const cleaned = cleanDefinition(definition({
        id: ' piqi.core ', semver: ' 1.0.0 ', title: '  ', owner: '',
        applicableContext: { fhirResources: [], workflowTags: [' ', ''] }
      }));

      expect(cleaned.id).toBe('piqi.core');
      expect(cleaned.semver).toBe('1.0.0');
      expect('title' in cleaned).toBeFalse();
      expect('owner' in cleaned).toBeFalse();
      // the service refuses an empty list where it accepts none at all
      expect('applicableContext' in cleaned).toBeFalse();
    });

    it('keeps the lists that have something in them, trimmed', () => {
      const cleaned = cleanDefinition(definition({ applicableContext: { fhirResources: [' Patient '], workflowTags: [] } }));

      expect(cleaned.applicableContext).toEqual({ fhirResources: ['Patient'] });
    });

    it('drops the roll-up for a policy that has none, and keeps it for one that needs it', () => {
      expect(cleanDefinition(definition({ scoringPolicy: { type: 'piqi-pass-fail', rollup: 'worst-of' } })).scoringPolicy)
        .toEqual({ type: 'piqi-pass-fail' });
      expect(cleanDefinition(definition()).scoringPolicy).toEqual({ type: 'piqi-dimension-scorecard', rollup: 'worst-of' });
    });

    it('numbers the checks by their place in the list, whatever they were numbered before', () => {
      const cleaned = cleanDefinition(definition({
        checks: [check({ id: 'b', ordinal: 9 }), check({ id: 'a' }), check({ id: 'c', ordinal: 1 })]
      }));

      expect(cleaned.checks.map(c => [c.id, c.ordinal])).toEqual([['b', 1], ['a', 2], ['c', 3]]);
    });

    it('sends each check only the settings its kind uses, without the empty ones', () => {
      const cleaned = cleanDefinition(definition({
        checks: [
          check({ id: 'e', parameters: { expression: ' Patient.name.exists() ', failureMessage: '', code: ' ', path: 'left over' } }),
          check({ id: 'v', type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { path: 'Patient.gender', valueSet: 'http://vs', system: '', expression: 'left over' } }),
          check({ id: 'p', type: 'FHIR_CONFORMANCE', parameters: { profiles: [' http://a ', ''] } }),
          check({ id: 'n', type: 'FHIR_CONFORMANCE', parameters: { profiles: [] } }),
          check({ id: 't', type: 'TERMINOLOGY', dimension: 'TERMINOLOGY', parameters: { validateCodings: false, valueSetWhitelistRegex: '' } })
        ]
      }));

      expect(cleaned.checks.map(c => c.parameters)).toEqual([
        { expression: 'Patient.name.exists()' },
        { path: 'Patient.gender', valueSet: 'http://vs' },
        { profiles: ['http://a'] },
        {},
        { validateCodings: false }
      ]);
    });

    it('defaults a check to enabled and a combined check to "any", and cleans its children', () => {
      const cleaned = cleanDefinition(definition({
        checks: [check({
          id: 'combo', type: 'VALUESET', dimension: 'TERMINOLOGY', enabled: undefined,
          parameters: { checks: [{ id: ' a ', type: 'FHIRPATH', parameters: { expression: ' true ', code: '' } }] }
        })]
      }));

      expect(cleaned.checks[0].enabled).toBeTrue();
      expect(cleaned.checks[0].parameters).toEqual({
        matchMode: 'ANY', checks: [{ id: 'a', type: 'FHIRPATH', parameters: { expression: 'true' } }]
      });
    });

    it('sends a check the form cannot edit exactly as it came', () => {
      const custom = check({ id: 'x', type: 'CUSTOM', parameters: { customCheckId: 'plugin', somethingElse: [1, 2] } });
      const deep = check({
        id: 'deep', type: 'VALUESET', dimension: 'TERMINOLOGY',
        parameters: { matchMode: 'ALL', checks: [{ id: 'inner', type: 'VALUESET', parameters: { checks: [{ id: 'leaf', type: 'FHIRPATH', parameters: { expression: 'true' } }] } }] }
      });

      const cleaned = cleanDefinition(definition({ checks: [custom, deep] }));

      expect(cleaned.checks[0].parameters).toEqual(custom.parameters);
      expect(cleaned.checks[1].parameters).toEqual(deep.parameters);
    });

    it('does not change the definition it was given', () => {
      const original = definition({ title: '  spaced  ' });
      const snapshot = cloneDefinition(original);

      cleanDefinition(original);

      expect(original).toEqual(snapshot);
    });
  });

  describe('sameDefinition', () => {
    it('ignores differences that would not be saved', () => {
      expect(sameDefinition(definition(), definition({ title: ' PIQI Core ', applicableContext: { fhirResources: [] } }))).toBeTrue();
    });

    it('sees a changed setting, and a changed order of checks', () => {
      expect(sameDefinition(definition(), definition({ owner: 'other' }))).toBeFalse();
      const two = [check({ id: 'a' }), check({ id: 'b' })];
      expect(sameDefinition(definition({ checks: two }), definition({ checks: [two[1], two[0]] }))).toBeFalse();
    });
  });

  describe('version numbers', () => {
    it('compares by number, not by text', () => {
      expect(compareVersions('1.10.0', '1.9.0')).toBeGreaterThan(0);
      expect(compareVersions('2.0.0', '10.0.0')).toBeLessThan(0);
      expect(compareVersions('1.2.3', '1.2.3')).toBe(0);
    });

    it('steps a part and zeroes the parts below it', () => {
      expect(bumpVersion('1.2.3', 'patch')).toBe('1.2.4');
      expect(bumpVersion('1.2.3', 'minor')).toBe('1.3.0');
      expect(bumpVersion('1.2.3', 'major')).toBe('2.0.0');
    });

    it('offers a step above the newest version, whatever order they are in', () => {
      expect(nextVersion(['1.2.0', '1.10.0', '1.9.4'])).toBe('1.10.1');
      expect(nextVersion(['1.2.0', '1.10.0'], 'minor')).toBe('1.11.0');
      expect(nextVersion(['1.2.0', '1.10.0'], 'major')).toBe('2.0.0');
    });

    it('starts at 1.0.0 when there is nothing to step from', () => {
      expect(nextVersion([])).toBe('1.0.0');
      expect(nextVersion(['not-a-version'])).toBe('1.0.0');
    });
  });

  describe('findIssues', () => {
    it('finds nothing wrong with a complete definition', () => {
      expect(findIssues(definition())).toEqual([]);
    });

    it('finds the problems with a blank new rubric', () => {
      expect(fields(newDefinition())).toEqual(['|id', '|checks']);
    });

    it('checks the id', () => {
      expect(fields(definition({ id: 'ab' }))).toEqual(['|id']);
      expect(fields(definition({ id: 'Has Spaces' }))).toEqual(['|id']);
      expect(fields(definition({ id: 'trailing.' }))).toEqual(['|id']);
      expect(fields(definition({ id: 'a'.repeat(129) }))).toEqual(['|id']);
      expect(fields(definition({ id: 'bundle-all-dimensions-v1' }))).toEqual([]);
    });

    it('checks the version', () => {
      expect(fields(definition({ semver: '' }))).toEqual(['|semver']);
      expect(fields(definition({ semver: '1.0' }))).toEqual(['|semver']);
      expect(fields(definition({ semver: '1.0.0-beta' }))).toEqual(['|semver']);
      expect(fields(definition({ semver: '0.0.0' }))).toEqual(['|semver']);
      expect(fields(definition({ semver: '0.0.1' }))).toEqual([]);
    });

    it('checks the lengths of the title and the owner', () => {
      expect(fields(definition({ title: 't'.repeat(257), owner: 'o'.repeat(129) }))).toEqual(['|title', '|owner']);
    });

    it('needs a dimension, a scoring policy, and a roll-up where the policy has one', () => {
      expect(fields(definition({ dimensions: [], checks: [] }))).toEqual(['|dimensions', '|checks']);
      expect(fields(definition({ scoringPolicy: { type: '' } }))).toEqual(['|scoringPolicy.type']);
      expect(fields(definition({ scoringPolicy: { type: 'piqi-check-scorecard' } }))).toEqual(['|scoringPolicy.rollup']);
      expect(fields(definition({ scoringPolicy: { type: 'piqi-pass-fail' } }))).toEqual([]);
    });

    it('checks what the rubric applies to', () => {
      const settings = { fhirResourceTypes: ['Patient', 'Observation'] } as IRubricSettings;
      expect(fields(definition({ applicableContext: { fhirResources: ['Patient', 'NotAThing'] } }), settings))
        .toEqual(['|applicableContext.fhirResources']);
      expect(fields(definition({ applicableContext: { fhirResources: ['Patient', 'Patient'] } }), settings))
        .toEqual(['|applicableContext.fhirResources']);
      expect(fields(definition({ applicableContext: { workflowTags: ['t'.repeat(129)] } })))
        .toEqual(['|applicableContext.workflowTags']);
      // without the service's list there is nothing to check a resource type against
      expect(fields(definition({ applicableContext: { fhirResources: ['NotAThing'] } }))).toEqual([]);
    });

    it('needs a check, at least one enabled, and no more than the limit', () => {
      expect(fields(definition({ checks: [] }))).toEqual(['|checks']);
      expect(fields(definition({ checks: [check({ enabled: false })] }))).toEqual(['|checks']);
      const many = Array.from({ length: 3 }, (_, i) => check({ id: `c${i}` }));
      expect(fields(definition({ checks: many }), { maxChecks: 2 } as IRubricSettings)).toEqual(['|checks']);
    });

    it('checks each check\'s id, and that no two share one', () => {
      expect(fields(definition({ checks: [check({ id: ' ' })] }))).toEqual(['0|id']);
      expect(fields(definition({ checks: [check(), check({ parameters: { expression: 'other' } })] }))).toEqual(['1|id']);
    });

    it('checks that a check\'s dimension is the one its type needs, and one the rubric has', () => {
      expect(fields(definition({ checks: [check({ type: 'COMPLETENESS', dimension: 'CONFORMANCE' })] }))).toEqual(['0|dimension']);
      expect(fields(definition({ checks: [check({ dimension: 'CURRENCY' })] }))).toEqual(['0|dimension']);
      expect(fields(definition({ checks: [check({ dimension: undefined })] }))).toEqual(['0|dimension']);
    });

    it('checks the settings of each kind of check', () => {
      expect(fields(definition({ checks: [check({ parameters: { expression: ' ' } })] }))).toEqual(['0|parameters.expression']);
      expect(fields(definition({ checks: [check({ parameters: { expression: 'x', code: 'c'.repeat(129) } })] })))
        .toEqual(['0|parameters.code']);
      expect(fields(definition({ checks: [check({ type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: {} })] })))
        .toEqual(['0|parameters.path', '0|parameters.valueSet']);
      expect(fields(definition({ checks: [check({ type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { path: 'p', valueSet: 'not a url', system: 'also not' } })] })))
        .toEqual(['0|parameters.valueSet', '0|parameters.system']);
      expect(fields(definition({ checks: [check({ type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { path: 'p', valueSet: 'http://hl7.org/fhir/ValueSet/x|4.0.1' } })] })))
        .toEqual([]);
      expect(fields(definition({ checks: [check({ type: 'FHIR_CONFORMANCE', parameters: { profiles: ['http://a', 'http://a', 'nope'] } })] })))
        .toEqual(['0|parameters.profiles', '0|parameters.profiles']);
      expect(fields(definition({ checks: [check({ type: 'TERMINOLOGY', dimension: 'TERMINOLOGY', parameters: { valueSetWhitelistRegex: '([' } })] })))
        .toEqual(['0|parameters.valueSetWhitelistRegex']);
    });

    it('checks the checks a combined check holds', () => {
      const combined = (children: IRubricCheck[]) =>
        definition({ checks: [check({ id: 'combo', type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { matchMode: 'ANY', checks: children } })] });

      expect(fields(combined([]))).toEqual(['0|parameters.checks']);
      expect(fields(combined([
        { id: '', type: 'FHIRPATH', parameters: {} },
        { id: 'a', type: 'VALUESET', parameters: { path: 'p' } },
        { id: 'a', type: 'FHIRPATH', parameters: { expression: 'true' } }
      ]))).toEqual([
        '0|parameters.checks[0].id', '0|parameters.checks[0].parameters.expression',
        '0|parameters.checks[1].parameters.valueSet',
        '0|parameters.checks[2].id'
      ]);
    });

    it('leaves a check the form cannot edit to the service', () => {
      expect(findIssues(definition({ checks: [check({ id: '', type: 'CUSTOM', dimension: undefined, parameters: {} }), check()] }))).toEqual([]);
    });

    it('names each problem the way the service would, by the check\'s id or its place', () => {
      const issues = findIssues(definition({ checks: [check({ id: '', parameters: {} }), check({ id: 'named', parameters: {} })] }));

      expect(issues.map(issue => issue.path)).toEqual([
        'checks[#1].id', 'checks[#1].parameters.expression', 'checks[named].parameters.expression']);
    });
  });

  describe('issuesFor and issuesForCheck', () => {
    const issues = findIssues(definition({ id: '', checks: [check({ id: '' }), check({ id: 'ok', parameters: {} })] }));

    it('picks out the problems of one field, of the rubric or of a check', () => {
      expect(issuesFor(issues, 'id').length).toBe(1);
      expect(issuesFor(issues, 'id', 0).length).toBe(1);
      expect(issuesFor(issues, 'id', 1)).toEqual([]);
    });

    it('picks out every problem of one check', () => {
      expect(issuesForCheck(issues, 1).map(issue => issue.field)).toEqual(['parameters.expression']);
    });
  });

  describe('placeServerError', () => {
    const draft = definition({
      checks: [
        check({ id: 'first' }),
        check({ id: 'combo', type: 'VALUESET', dimension: 'TERMINOLOGY', parameters: { checks: [{ id: 'child', type: 'FHIRPATH', parameters: {} }] } })
      ]
    });

    it('places an error about the rubric on its field', () => {
      expect(placeServerError('semver: 0.0.0 is reserved', draft)).toEqual({ path: 'semver', message: '0.0.0 is reserved', field: 'semver' });
      expect(placeServerError('scoringPolicy.rollup: is required and must be one of [worst-of]', draft).field).toBe('scoringPolicy.rollup');
      expect(placeServerError('scoringPolicy: is required', draft).field).toBe('scoringPolicy.type');
      expect(placeServerError('checks: at least one check must be enabled', draft).field).toBe('checks');
    });

    it('places an error about one entry of a list on the list', () => {
      expect(placeServerError('applicableContext.fhirResources[1]: \'Nope\' is not a valid FHIR R4 resource type', draft).field)
        .toBe('applicableContext.fhirResources');
    });

    it('finds a check by its id', () => {
      expect(placeServerError('checks[first].parameters.code: must be at most 128 characters', draft))
        .toEqual(jasmine.objectContaining({ checkIndex: 0, field: 'parameters.code', message: 'must be at most 128 characters' }));
    });

    it('finds a check by its place when the service had no id to name it by', () => {
      expect(placeServerError('checks[1].id: must not be blank', draft)).toEqual(jasmine.objectContaining({ checkIndex: 1, field: 'id' }));
    });

    it('puts an error about a child of a combined check with its parent', () => {
      expect(placeServerError('checks[combo.child]: id is required', draft).checkIndex).toBe(1);
    });

    it('works out the field from the message when the service names only the check', () => {
      expect(placeServerError('checks[first]: FHIRPATH requires parameters.expression', draft).field).toBe('parameters.expression');
      expect(placeServerError('checks[first]: invalid FHIRPath expression: unexpected token', draft).field).toBe('parameters.expression');
      expect(placeServerError('checks[first]: duplicate check id', draft).field).toBe('id');
      expect(placeServerError('checks[first]: dimension CURRENCY is not declared in the rubric\'s dimensions', draft).field).toBe('dimension');
      expect(placeServerError('checks[first]: severityOverride is required', draft).field).toBe('severityOverride');
    });

    it('keeps an error it cannot place, whole', () => {
      expect(placeServerError('checks[gone]: something', draft)).toEqual({ path: 'checks[gone]', message: 'something', field: '' });
      expect(placeServerError('payload: unknown property \'colour\'', draft)).toEqual({ path: 'payload', message: 'unknown property \'colour\'', field: '' });
      expect(placeServerError('Malformed YAML rubric payload', draft)).toEqual({ path: '', message: 'Malformed YAML rubric payload', field: '' });
    });
  });

  it('rollupRequired follows the service\'s policies when it has them', () => {
    const settings = { scoringPolicies: [{ type: 'new-policy', rollupRequired: false }] } as IRubricSettings;
    expect(rollupRequired('new-policy', settings)).toBeFalse();
    expect(rollupRequired('piqi-pass-fail')).toBeFalse();
    expect(rollupRequired('piqi-check-scorecard')).toBeTrue();
  });
});
