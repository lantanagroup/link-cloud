import {describe, expect, it} from 'vitest';
import type {MrnIdentifierRule} from '../../../api/contracts';
import {
  findRuleForElement,
  operatorNeedsValue,
  operatorsForElement,
  pruneObservations,
  removeRuleByElement,
  ruleCountForPatient,
  upsertPatientRule,
  withOrdinals,
  type MrnObservation
} from './rules';

describe('operatorsForElement', () => {
  it('gives every element must-be-present and equals', () => {
    (['value', 'type', 'system', 'use', 'assigner'] as const).forEach(element => {
      const keys = operatorsForElement(element).map(def => def.key);
      expect(keys).toEqual(['must_be_present', 'equals']);
    });
  });

  it('adds greater-than/less-than to periodStart only', () => {
    expect(operatorsForElement('periodStart').map(def => def.key)).toEqual([
      'must_be_present',
      'equals',
      'greater_than',
      'less_than'
    ]);
  });

  it('adds greater-than/less-than/absent to periodEnd, matching the POC over the trimmed AC list', () => {
    expect(operatorsForElement('periodEnd').map(def => def.key)).toEqual([
      'must_be_present',
      'equals',
      'greater_than',
      'less_than',
      'absent'
    ]);
  });
});

describe('operatorNeedsValue', () => {
  it('is false for must_be_present and absent', () => {
    expect(operatorNeedsValue('system', 'must_be_present')).toBe(false);
    expect(operatorNeedsValue('periodEnd', 'absent')).toBe(false);
  });

  it('is true for equals/greater_than/less_than', () => {
    expect(operatorNeedsValue('system', 'equals')).toBe(true);
    expect(operatorNeedsValue('periodStart', 'greater_than')).toBe(true);
    expect(operatorNeedsValue('periodEnd', 'less_than')).toBe(true);
  });
});

function rule(overrides: Partial<MrnIdentifierRule> = {}): MrnIdentifierRule {
  return {ordinal: 0, element: 'system', rule: 'equals', value: 'http://example.org', ...overrides};
}

describe('withOrdinals', () => {
  it('restamps ordinals from array position', () => {
    const rules = [rule({ordinal: 9}), rule({ordinal: 2}), rule({ordinal: 5})];
    expect(withOrdinals(rules).map(r => r.ordinal)).toEqual([0, 1, 2]);
  });
});

describe('findRuleForElement / ruleCountForPatient', () => {
  it('finds the rule for this exact patient + identifier + element only', () => {
    const rules = [
      rule({element: 'value', patientId: 'p1', identifierIndex: 0}),
      rule({element: 'value', patientId: 'p1', identifierIndex: 1, value: 'other'})
    ];
    expect(findRuleForElement(rules, 'p1', 0, 'value')).toBe(rules[0]);
    expect(findRuleForElement(rules, 'p1', 1, 'value')).toBe(rules[1]);
    expect(findRuleForElement(rules, 'p1', 2, 'value')).toBeUndefined();
    expect(findRuleForElement(rules, 'p2', 0, 'value')).toBeUndefined();
  });

  it('does not match a rule added through the plain rule list (no patient/identifier link)', () => {
    const rules = [rule({element: 'system'})]; // no patientId/identifierIndex
    expect(findRuleForElement(rules, 'p1', 0, 'system')).toBeUndefined();
  });

  it('counts only observed elements that still have a matching rule of this same patient', () => {
    const rules = [rule({element: 'system', patientId: 'p1', identifierIndex: 0})];
    const observations: MrnObservation[] = [
      {patientId: 'p1', elements: ['system', 'use']}, // 'use' has no rule
      {patientId: 'p2', elements: ['system']} // rule belongs to p1, not p2
    ];
    expect(ruleCountForPatient(rules, observations, 'p1')).toBe(1);
    expect(ruleCountForPatient(rules, observations, 'p2')).toBe(0);
    expect(ruleCountForPatient(rules, observations, 'p3')).toBe(0);
  });

  it('counts the same element on two identifiers of the patient as two rules', () => {
    const rules = [
      rule({element: 'value', patientId: 'p1', identifierIndex: 0}),
      rule({element: 'assigner', patientId: 'p1', identifierIndex: 0}),
      rule({element: 'value', patientId: 'p1', identifierIndex: 1}),
      rule({element: 'assigner', patientId: 'p1', identifierIndex: 1})
    ];
    const observations: MrnObservation[] = [{patientId: 'p1', elements: ['value', 'assigner']}];
    expect(ruleCountForPatient(rules, observations, 'p1')).toBe(4);
  });
});

describe('upsertPatientRule', () => {
  it('creates a rule scoped to the patient and identifier index, and records the observation', () => {
    const {rules, observations} = upsertPatientRule([], [], 'p1', 0, 'system', 'equals', 'http://x');
    expect(rules).toEqual([
      {ordinal: 0, element: 'system', rule: 'equals', value: 'http://x', patientId: 'p1', identifierIndex: 0}
    ]);
    expect(observations).toEqual([{patientId: 'p1', elements: ['system']}]);
  });

  it('updates the existing rule for that identifier rather than duplicating it', () => {
    const first = upsertPatientRule([], [], 'p1', 0, 'system', 'equals', 'http://x');
    const second = upsertPatientRule(first.rules, first.observations, 'p1', 0, 'system', 'must_be_present', '');
    expect(second.rules).toHaveLength(1);
    expect(second.rules[0]).toMatchObject({element: 'system', rule: 'must_be_present', value: ''});
  });

  it('keeps a second identifier of the same patient independent, not shared with the first', () => {
    const first = upsertPatientRule([], [], 'p1', 0, 'system', 'equals', 'http://x');
    const second = upsertPatientRule(first.rules, first.observations, 'p1', 1, 'system', 'equals', 'http://y');

    expect(second.rules).toHaveLength(2);
    expect(second.rules[0]).toMatchObject({identifierIndex: 0, value: 'http://x'});
    expect(second.rules[1]).toMatchObject({identifierIndex: 1, value: 'http://y'});
    // Both rules belong to the same patient, so the observation collapses to one entry.
    expect(second.observations).toEqual([{patientId: 'p1', elements: ['system']}]);
  });

  it('lets a second patient contribute to the same element without touching the first patient\'s rule', () => {
    const first = upsertPatientRule([], [], 'p1', 0, 'system', 'equals', 'http://x');
    const second = upsertPatientRule(first.rules, first.observations, 'p2', 0, 'system', 'equals', 'http://x');
    expect(second.rules).toHaveLength(2);
    expect(second.observations).toEqual([
      {patientId: 'p1', elements: ['system']},
      {patientId: 'p2', elements: ['system']}
    ]);
  });
});

describe('removeRuleByElement / pruneObservations', () => {
  it('removes only the targeted identifier\'s rule, leaving the same element on another identifier intact', () => {
    const rules = [
      rule({element: 'system', ordinal: 0, patientId: 'p1', identifierIndex: 0}),
      rule({element: 'system', ordinal: 1, patientId: 'p1', identifierIndex: 1, value: 'http://y'})
    ];
    const observations: MrnObservation[] = [{patientId: 'p1', elements: ['system']}];

    const result = removeRuleByElement(rules, observations, 'p1', 0, 'system');

    expect(result.rules).toHaveLength(1);
    expect(result.rules[0]).toMatchObject({identifierIndex: 1, value: 'http://y'});
    // The patient still has a rule for 'system' (on the other identifier), so it stays observed.
    expect(result.observations).toEqual([{patientId: 'p1', elements: ['system']}]);
  });

  it('drops the observation once the patient has no remaining rule for that element', () => {
    const rules = [rule({element: 'system', ordinal: 0, patientId: 'p1', identifierIndex: 0})];
    const observations: MrnObservation[] = [{patientId: 'p1', elements: ['system']}];

    const result = removeRuleByElement(rules, observations, 'p1', 0, 'system');

    expect(result.rules).toEqual([]);
    expect(result.observations).toEqual([]);
  });

  it('pruneObservations drops an element once no rule of that patient targets it, keeping unrelated elements', () => {
    const rules = [rule({element: 'value', patientId: 'p1', identifierIndex: 0})];
    const observations: MrnObservation[] = [{patientId: 'p1', elements: ['value', 'system']}];
    expect(pruneObservations(rules, observations)).toEqual([{patientId: 'p1', elements: ['value']}]);
  });
});
