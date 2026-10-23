import {describe, expect, it} from 'vitest';
import {defaultMrnIntake, isMrnIntakeComplete, toMrnIntake, validateMrnIntake, type MrnIntakeDraft} from './validate';

function withRule(draft: MrnIntakeDraft): MrnIntakeDraft {
  return {...draft, rules: [{ordinal: 0, element: 'system', rule: 'equals', value: 'http://example.org'}]};
}

/** The minimum draft that passes when hasMultipleMrn is No — Question 2 never applies. */
function minimalNoDraft(): MrnIntakeDraft {
  return withRule({
    ...defaultMrnIntake(),
    hasMultipleMrn: false,
    variesByFacility: false,
    changesOverTime: false
  });
}

describe('validateMrnIntake', () => {
  it('requires an answer to every top-level yes/no question', () => {
    const errors = validateMrnIntake(defaultMrnIntake());
    expect(errors.hasMultipleMrn).toBe('onboarding:mrnIntake.errors.selectYesNo');
    expect(errors.rules).toBe('onboarding:mrnIntake.errors.addAtLeastOneRule');
    expect(errors.variesByFacility).toBe('onboarding:mrnIntake.errors.selectYesNo');
    expect(errors.changesOverTime).toBe('onboarding:mrnIntake.errors.selectYesNo');
  });

  it('passes the minimal all-No draft once a rule exists', () => {
    expect(validateMrnIntake(minimalNoDraft())).toEqual({});
  });

  it('does not require Question 2 fields when hasMultipleMrn is No', () => {
    const draft = minimalNoDraft();
    const errors = validateMrnIntake(draft);
    expect(errors.userFacingIdentifierNames).toBeUndefined();
    expect(errors.isCalledMrn).toBeUndefined();
    expect(errors.canSearchByIdentifier).toBeUndefined();
  });

  it('requires Question 2 fields once hasMultipleMrn is Yes', () => {
    const draft = withRule({...defaultMrnIntake(), hasMultipleMrn: true, variesByFacility: false, changesOverTime: false});
    const errors = validateMrnIntake(draft);
    expect(errors.multipleMrnTypes).toBe('onboarding:mrnIntake.errors.selectAtLeastOne');
    expect(errors.userFacingIdentifierNames).toBe('onboarding:mrnIntake.errors.selectAtLeastOne');
    expect(errors.isCalledMrn).toBe('onboarding:mrnIntake.errors.selectYesNo');
    expect(errors.canSearchByIdentifier).toBe('onboarding:mrnIntake.errors.selectYesNo');
  });

  it('requires the other-identifier text once "other" is checked', () => {
    const draft = withRule({
      ...defaultMrnIntake(),
      hasMultipleMrn: true,
      multipleMrnTypes: ['other'],
      userFacingIdentifierNames: ['other'],
      isCalledMrn: true,
      canSearchByIdentifier: true,
      variesByFacility: false,
      changesOverTime: false
    });
    expect(validateMrnIntake(draft).multipleMrnOtherText).toBe('onboarding:mrnIntake.errors.describeOtherIdentifier');
    expect(validateMrnIntake({...draft, multipleMrnOtherText: 'Loyalty card id'}).multipleMrnOtherText).toBeUndefined();
  });

  it('requires the "what term is used" text when isCalledMrn is No', () => {
    const draft = withRule({
      ...defaultMrnIntake(),
      hasMultipleMrn: true,
      multipleMrnTypes: ['empi'],
      userFacingIdentifierNames: ['empi'],
      isCalledMrn: false,
      canSearchByIdentifier: true,
      variesByFacility: false,
      changesOverTime: false
    });
    expect(validateMrnIntake(draft).otherTermUsed).toBe('onboarding:mrnIntake.errors.describeOtherTerm');
  });

  it('requires at least one rule, then a value on every rule that needs one', () => {
    const noRules = minimalNoDraft();
    expect(validateMrnIntake({...noRules, rules: []}).rules).toBe('onboarding:mrnIntake.errors.addAtLeastOneRule');

    const missingValue = {
      ...noRules,
      rules: [{ordinal: 0, element: 'system' as const, rule: 'equals', value: ''}]
    };
    expect(validateMrnIntake(missingValue).ruleValues).toBe('onboarding:mrnIntake.errors.ruleValueRequired');

    const presentOnly = {
      ...noRules,
      rules: [{ordinal: 0, element: 'system' as const, rule: 'must_be_present', value: ''}]
    };
    expect(validateMrnIntake(presentOnly).ruleValues).toBeUndefined();
  });

  it('requires variance/change types and other-text the same way as Question 1', () => {
    const draft = withRule({...minimalNoDraft(), variesByFacility: true, changesOverTime: true});
    const errors = validateMrnIntake(draft);
    expect(errors.varianceTypes).toBe('onboarding:mrnIntake.errors.selectAtLeastOne');
    expect(errors.changeTypes).toBe('onboarding:mrnIntake.errors.selectAtLeastOne');

    const withOther = {
      ...draft,
      varianceTypes: ['other' as const],
      changeTypes: ['other' as const]
    };
    expect(validateMrnIntake(withOther).varianceOtherText).toBe('onboarding:mrnIntake.errors.describeOtherVariance');
    expect(validateMrnIntake(withOther).changeOtherText).toBe('onboarding:mrnIntake.errors.describeOtherChange');
  });
});

describe('isMrnIntakeComplete', () => {
  it('is false for undefined and an incomplete draft, true once valid', () => {
    expect(isMrnIntakeComplete(undefined)).toBe(false);
    expect(isMrnIntakeComplete(defaultMrnIntake())).toBe(false);
    expect(isMrnIntakeComplete(minimalNoDraft())).toBe(true);
  });
});

describe('toMrnIntake', () => {
  it('defaults inapplicable yes/no answers to false rather than leaving them undefined', () => {
    const intake = toMrnIntake(minimalNoDraft());
    expect(intake.isCalledMrn).toBe(false);
    expect(intake.canSearchByIdentifier).toBe(false);
  });

  it('carries real answers through unchanged', () => {
    const draft = withRule({
      ...defaultMrnIntake(),
      hasMultipleMrn: true,
      multipleMrnTypes: ['empi'],
      userFacingIdentifierNames: ['empi'],
      isCalledMrn: true,
      canSearchByIdentifier: false,
      variesByFacility: false,
      changesOverTime: false
    });
    const intake = toMrnIntake(draft);
    expect(intake.hasMultipleMrn).toBe(true);
    expect(intake.isCalledMrn).toBe(true);
    expect(intake.canSearchByIdentifier).toBe(false);
  });
});
