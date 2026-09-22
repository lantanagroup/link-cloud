import type {MrnIdentifierRule, MrnIntake} from '../../../api/contracts';
import {operatorNeedsValue, type MrnRuleElement} from './rules';

export interface FieldErrors {
  [field: string]: string; // i18n keys, not sentences
}

/**
 * The step's own working copy. The wire contract's yes/no answers are plain `boolean` (there is
 * nothing to save until they're answered), but the form itself needs to distinguish "unanswered"
 * from "No" while the user is still working through it — `YesNoField` already renders `undefined`
 * as neither choice selected.
 */
export type MrnIntakeDraft = Omit<
  MrnIntake,
  'hasMultipleMrn' | 'isCalledMrn' | 'canSearchByIdentifier' | 'variesByFacility' | 'changesOverTime'
> & {
  hasMultipleMrn?: boolean;
  isCalledMrn?: boolean;
  canSearchByIdentifier?: boolean;
  variesByFacility?: boolean;
  changesOverTime?: boolean;
};

export function defaultMrnIntake(): MrnIntakeDraft {
  return {
    multipleMrnTypes: [],
    userFacingIdentifierNames: [],
    variesByFacility: undefined,
    varianceTypes: [],
    changesOverTime: undefined,
    changeTypes: [],
    rules: [],
    observations: []
  };
}

function ruleNeedsValue(rule: MrnIdentifierRule): boolean {
  return operatorNeedsValue(rule.element as MrnRuleElement, rule.rule);
}

/**
 * Mirrors the POC's `validateMrnIntakeFields` (`index.html:5601-5644`) — same fields, same
 * conditional visibility, same "Question 2" (user-facing identifier) section gated on
 * `hasMultipleMrn === true`.
 */
export function validateMrnIntake(draft: MrnIntakeDraft): FieldErrors {
  const errors: FieldErrors = {};

  if (draft.hasMultipleMrn === undefined) {
    errors.hasMultipleMrn = 'onboarding:mrnIntake.errors.selectYesNo';
  }

  if (draft.hasMultipleMrn === true) {
    if (draft.multipleMrnTypes.length === 0) {
      errors.multipleMrnTypes = 'onboarding:mrnIntake.errors.selectAtLeastOne';
    }
    if (draft.multipleMrnTypes.includes('other') && !draft.multipleMrnOtherText?.trim()) {
      errors.multipleMrnOtherText = 'onboarding:mrnIntake.errors.describeOtherIdentifier';
    }

    if (draft.userFacingIdentifierNames.length === 0) {
      errors.userFacingIdentifierNames = 'onboarding:mrnIntake.errors.selectAtLeastOne';
    }
    if (draft.isCalledMrn === undefined) {
      errors.isCalledMrn = 'onboarding:mrnIntake.errors.selectYesNo';
    }
    if (draft.isCalledMrn === false && !draft.otherTermUsed?.trim()) {
      errors.otherTermUsed = 'onboarding:mrnIntake.errors.describeOtherTerm';
    }
    if (draft.canSearchByIdentifier === undefined) {
      errors.canSearchByIdentifier = 'onboarding:mrnIntake.errors.selectYesNo';
    }
  }

  if (draft.rules.length === 0) {
    errors.rules = 'onboarding:mrnIntake.errors.addAtLeastOneRule';
  } else if (draft.rules.some(rule => ruleNeedsValue(rule) && !rule.value.trim())) {
    errors.ruleValues = 'onboarding:mrnIntake.errors.ruleValueRequired';
  }

  if (draft.variesByFacility === undefined) {
    errors.variesByFacility = 'onboarding:mrnIntake.errors.selectYesNo';
  }
  if (draft.variesByFacility === true) {
    if (draft.varianceTypes.length === 0) {
      errors.varianceTypes = 'onboarding:mrnIntake.errors.selectAtLeastOne';
    }
    if (draft.varianceTypes.includes('other') && !draft.varianceOtherText?.trim()) {
      errors.varianceOtherText = 'onboarding:mrnIntake.errors.describeOtherVariance';
    }
  }

  if (draft.changesOverTime === undefined) {
    errors.changesOverTime = 'onboarding:mrnIntake.errors.selectYesNo';
  }
  if (draft.changesOverTime === true) {
    if (draft.changeTypes.length === 0) {
      errors.changeTypes = 'onboarding:mrnIntake.errors.selectAtLeastOne';
    }
    if (draft.changeTypes.includes('other') && !draft.changeOtherText?.trim()) {
      errors.changeOtherText = 'onboarding:mrnIntake.errors.describeOtherChange';
    }
  }

  return errors;
}

export function isMrnIntakeComplete(intake: MrnIntakeDraft | undefined): boolean {
  return Boolean(intake) && Object.keys(validateMrnIntake(intake!)).length === 0;
}

/**
 * Only called once `validateMrnIntake` reports no errors, so every `??` fallback below fires
 * exclusively for a yes/no question that was never applicable (its section stayed hidden because
 * an earlier answer was No) — never for one the user skipped.
 */
export function toMrnIntake(draft: MrnIntakeDraft): MrnIntake {
  return {
    hasMultipleMrn: draft.hasMultipleMrn ?? false,
    multipleMrnTypes: draft.multipleMrnTypes,
    multipleMrnOtherText: draft.multipleMrnOtherText,
    userFacingIdentifierNames: draft.userFacingIdentifierNames,
    isCalledMrn: draft.isCalledMrn ?? false,
    otherTermUsed: draft.otherTermUsed,
    canSearchByIdentifier: draft.canSearchByIdentifier ?? false,
    variesByFacility: draft.variesByFacility ?? false,
    varianceTypes: draft.varianceTypes,
    varianceOtherText: draft.varianceOtherText,
    changesOverTime: draft.changesOverTime ?? false,
    changeTypes: draft.changeTypes,
    changeOtherText: draft.changeOtherText,
    rules: draft.rules,
    observations: draft.observations
  };
}
