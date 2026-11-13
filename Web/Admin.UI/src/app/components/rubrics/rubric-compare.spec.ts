import { IRubricDefinition } from '../../interfaces/validation/rubric.interface';
import { compareDefinitions } from './rubric-compare';

const base: IRubricDefinition = {
  id: 'piqi.core',
  semver: '1.0.0',
  title: 'PIQI Core',
  owner: 'lcg',
  dimensions: ['CONFORMANCE'],
  scoringPolicy: { type: 'piqi-pass-fail' },
  checks: [
    { id: 'name', type: 'FHIRPATH', dimension: 'CONFORMANCE', severityOverride: 'ERROR', ordinal: 1,
      parameters: { expression: 'Patient.name.exists()', failureMessage: 'No name' } },
    { id: 'gender', type: 'VALUESET', dimension: 'CONFORMANCE', severityOverride: 'WARNING', ordinal: 2,
      parameters: { path: 'Patient.gender', valueSet: 'http://vs' } },
    { id: 'either', type: 'VALUESET', dimension: 'CONFORMANCE', severityOverride: 'ERROR', ordinal: 3,
      parameters: { matchMode: 'ANY', checks: [{ id: 'a', type: 'FHIRPATH', parameters: { expression: 'true' } }] } }
  ]
};

/** A copy of the base definition with something changed in it. */
function changed(change: (definition: IRubricDefinition) => void): IRubricDefinition {
  const copy: IRubricDefinition = JSON.parse(JSON.stringify(base));
  copy.semver = '1.1.0';
  change(copy);
  return copy;
}

describe('compareDefinitions', () => {
  it('finds nothing between a definition and a copy under another version number', () => {
    const result = compareDefinitions(base, changed(() => undefined));

    expect(result.identical).toBeTrue();
    expect(result.unchangedCount).toBe(3);
  });

  it('lists the rubric settings that differ, worded as the screens word them', () => {
    const result = compareDefinitions(base, changed(definition => {
      definition.title = 'PIQI Core v2';
      definition.dimensions = ['CONFORMANCE', 'TERMINOLOGY'];
      definition.scoringPolicy = { type: 'piqi-dimension-scorecard', rollup: 'worst-of' };
      definition.applicableContext = { fhirResources: ['Patient', 'Encounter'] };
    }));

    expect(result.settings).toEqual([
      { label: 'Title', before: 'PIQI Core', after: 'PIQI Core v2' },
      { label: 'Dimensions', before: 'Conformance', after: 'Conformance, Terminology' },
      { label: 'Scoring', before: 'Pass/fail', after: 'Dimension scorecard, worst of' },
      { label: 'Resource types', before: '', after: 'Patient, Encounter' }
    ]);
    expect(result.identical).toBeFalse();
  });

  it('finds added and removed checks by their id', () => {
    const result = compareDefinitions(base, changed(definition => {
      definition.checks = definition.checks.filter(check => check.id !== 'gender');
      definition.checks.push({ id: 'birthdate', type: 'FHIRPATH', dimension: 'CONFORMANCE', ordinal: 4,
        parameters: { expression: 'Patient.birthDate.exists()' } });
    }));

    expect(result.added.map(check => check.id)).toEqual(['birthdate']);
    expect(result.removed.map(check => check.id)).toEqual(['gender']);
    expect(result.changed).toEqual([]);
    expect(result.unchangedCount).toBe(2);
    expect(result.reordered).toBeFalse();
  });

  it('says what changed in a check both have: its own fields and its settings', () => {
    const result = compareDefinitions(base, changed(definition => {
      const name = definition.checks[0];
      name.severityOverride = 'WARNING';
      name.enabled = false;
      name.parameters = { expression: 'Patient.name.family.exists()', code: 'no-family-name' };
    }));

    expect(result.changed).toEqual([{
      id: 'name',
      changes: [
        { label: 'Severity', before: 'Error', after: 'Warning' },
        { label: 'Enabled', before: 'Yes', after: 'No' },
        { label: 'Expression', before: 'Patient.name.exists()', after: 'Patient.name.family.exists()' },
        { label: 'Code', before: '', after: 'no-family-name' },
        { label: 'Failure message', before: 'No name', after: '' }
      ]
    }]);
  });

  it('treats a check with no enabled flag as enabled, so writing the flag out changes nothing', () => {
    const result = compareDefinitions(base, changed(definition => {
      definition.checks[0].enabled = true;
    }));

    expect(result.identical).toBeTrue();
  });

  it('notices a change inside a combined check\'s children', () => {
    const result = compareDefinitions(base, changed(definition => {
      definition.checks[2].parameters!.matchMode = 'ALL';
      definition.checks[2].parameters!.checks!.push({ id: 'b', type: 'FHIRPATH', parameters: { expression: 'false' } });
    }));

    expect(result.changed.length).toBe(1);
    expect(result.changed[0].id).toBe('either');
    expect(result.changed[0].changes[0]).toEqual(
      { label: 'Passes when', before: 'Any of its checks passes', after: 'All of its checks pass' });
    expect(result.changed[0].changes[1]).toEqual({
      label: 'Child checks',
      before: 'a (FHIRPath): true',
      after: 'a (FHIRPath): true\nb (FHIRPath): false'
    });
  });

  it('says when the checks both have run in another order, and not when one was only added or removed', () => {
    const moved = compareDefinitions(base, changed(definition => {
      definition.checks[0].ordinal = 2;
      definition.checks[1].ordinal = 1;
    }));
    const removedFirst = compareDefinitions(base, changed(definition => {
      definition.checks = definition.checks.slice(1);
    }));

    expect(moved.reordered).toBeTrue();
    expect(moved.changed).toEqual([]);
    expect(moved.identical).toBeFalse();
    expect(removedFirst.reordered).toBeFalse();
  });

  it('goes the other way when the sides are swapped', () => {
    const newer = changed(definition => {
      definition.checks = definition.checks.slice(0, 2);
    });

    expect(compareDefinitions(base, newer).removed.map(check => check.id)).toEqual(['either']);
    expect(compareDefinitions(newer, base).added.map(check => check.id)).toEqual(['either']);
  });
});
