import type {MrnIdentifierRule, PatientIdentifierElement} from '../../../api/contracts';

/**
 * The seven `Patient.identifier` fields a rule can target, in the POC's display order
 * (`index.html:5011-5019`). Kept as `keyof PatientIdentifierElement` so the two can never drift.
 */
export type MrnRuleElement = keyof PatientIdentifierElement;

export type MrnRuleOperator = 'must_be_present' | 'equals' | 'greater_than' | 'less_than' | 'absent';

export interface MrnRuleElementDef {
  key: MrnRuleElement;
  labelKey: string;
}

export interface MrnRuleOperatorDef {
  key: MrnRuleOperator;
  labelKey: string;
  needsValue: boolean;
}

export const MRN_RULE_ELEMENTS: readonly MrnRuleElementDef[] = [
  {key: 'value', labelKey: 'onboarding:mrnIntake.rules.elements.value'},
  {key: 'type', labelKey: 'onboarding:mrnIntake.rules.elements.type'},
  {key: 'system', labelKey: 'onboarding:mrnIntake.rules.elements.system'},
  {key: 'use', labelKey: 'onboarding:mrnIntake.rules.elements.use'},
  {key: 'assigner', labelKey: 'onboarding:mrnIntake.rules.elements.assigner'},
  {key: 'periodStart', labelKey: 'onboarding:mrnIntake.rules.elements.periodStart'},
  {key: 'periodEnd', labelKey: 'onboarding:mrnIntake.rules.elements.periodEnd'}
];

const BASE_OPERATORS: readonly MrnRuleOperatorDef[] = [
  {key: 'must_be_present', labelKey: 'onboarding:mrnIntake.rules.operators.mustBePresent', needsValue: false},
  {key: 'equals', labelKey: 'onboarding:mrnIntake.rules.operators.equals', needsValue: true}
];

const RANGE_OPERATORS: readonly MrnRuleOperatorDef[] = [
  {key: 'greater_than', labelKey: 'onboarding:mrnIntake.rules.operators.greaterThan', needsValue: true},
  {key: 'less_than', labelKey: 'onboarding:mrnIntake.rules.operators.lessThan', needsValue: true}
];

const ABSENT_OPERATOR: MrnRuleOperatorDef = {
  key: 'absent',
  labelKey: 'onboarding:mrnIntake.rules.operators.absent',
  needsValue: false
};

/**
 * Per-element operator lists, matching the POC's `MRN_RULE_OPERATOR_DEFS`
 * (`index.html:5020-5054`) rather than the story's trimmed AC10 list: every element gets
 * must-be-present/equals, the two period elements additionally get greater-than/less-than, and
 * `periodEnd` alone also gets "Must be absent (no end date)".
 */
export function operatorsForElement(element: MrnRuleElement): readonly MrnRuleOperatorDef[] {
  if (element === 'periodStart') {
    return [...BASE_OPERATORS, ...RANGE_OPERATORS];
  }
  if (element === 'periodEnd') {
    return [...BASE_OPERATORS, ...RANGE_OPERATORS, ABSENT_OPERATOR];
  }
  return BASE_OPERATORS;
}

export function operatorNeedsValue(element: MrnRuleElement, operator: string): boolean {
  return operatorsForElement(element).find(def => def.key === operator)?.needsValue ?? true;
}

const PLACEHOLDER_KEYS: Record<MrnRuleElement, string> = {
  value: 'onboarding:mrnIntake.rules.placeholders.value',
  type: 'onboarding:mrnIntake.rules.placeholders.type',
  system: 'onboarding:mrnIntake.rules.placeholders.system',
  use: 'onboarding:mrnIntake.rules.placeholders.use',
  assigner: 'onboarding:mrnIntake.rules.placeholders.assigner',
  periodStart: 'onboarding:mrnIntake.rules.placeholders.period',
  periodEnd: 'onboarding:mrnIntake.rules.placeholders.period'
};

export function placeholderKeyForElement(element: MrnRuleElement): string {
  return PLACEHOLDER_KEYS[element];
}

/** Plain field names for the patient identifier display (AC7) — distinct from the dotted
 * `identifier.*` labels above, which name the same seven keys for the rule builder (AC9). */
export const IDENTIFIER_FIELD_LABEL_KEYS: Record<MrnRuleElement, string> = {
  value: 'onboarding:mrnIntake.identifierTable.fields.value',
  type: 'onboarding:mrnIntake.identifierTable.fields.type',
  system: 'onboarding:mrnIntake.identifierTable.fields.system',
  use: 'onboarding:mrnIntake.identifierTable.fields.use',
  assigner: 'onboarding:mrnIntake.identifierTable.fields.assigner',
  periodStart: 'onboarding:mrnIntake.identifierTable.fields.periodStart',
  periodEnd: 'onboarding:mrnIntake.identifierTable.fields.periodEnd'
};

/** Reads the field an element key names off a patient's identifier — `undefined` when absent. */
export function identifierElementValue(
  element: MrnRuleElement,
  identifier: PatientIdentifierElement
): string | undefined {
  return identifier[element];
}

export function isMrnRuleElement(value: string): value is MrnRuleElement {
  return MRN_RULE_ELEMENTS.some(def => def.key === value);
}

// ---------------------------------------------------------------- patient <-> rule linking

/** Which element keys were added to `rules` while a given patient's identifiers were on screen. */
export interface MrnObservation {
  patientId: string;
  elements: string[];
}

/** Restamps ordinals from array position, so a reorder or removal always saves a dense sequence. */
export function withOrdinals(rules: readonly MrnIdentifierRule[]): MrnIdentifierRule[] {
  return rules.map((rule, index) => ({...rule, ordinal: index}));
}

/** The rule for `element` on this exact patient's identifier — never another identifier of the
 * same patient, and never another patient's. */
export function findRuleForElement(
  rules: readonly MrnIdentifierRule[],
  patientId: string,
  identifierIndex: number,
  element: MrnRuleElement
): MrnIdentifierRule | undefined {
  return rules.find(
    rule => rule.patientId === patientId && rule.identifierIndex === identifierIndex && rule.element === element
  );
}

/** How many of this patient's contributed elements still have a matching rule of theirs (the
 * table's badge) — a rule belonging to a different patient never counts here. */
export function ruleCountForPatient(
  rules: readonly MrnIdentifierRule[],
  observations: readonly MrnObservation[],
  patientId: string
): number {
  const observed = observations.find(o => o.patientId === patientId)?.elements ?? [];
  return observed.filter(element => rules.some(rule => rule.patientId === patientId && rule.element === element))
    .length;
}

/**
 * Drops any element from any patient's observation list once none of that same patient's rules
 * target it anymore — the generic "keep both sides in sync" step, run after any edit to `rules`
 * regardless of whether the edit came from the patient view or the plain rule list.
 */
export function pruneObservations(
  rules: readonly MrnIdentifierRule[],
  observations: readonly MrnObservation[]
): MrnObservation[] {
  return observations
    .map(observation => ({
      ...observation,
      elements: observation.elements.filter(element =>
        rules.some(rule => rule.patientId === observation.patientId && rule.element === element)
      )
    }))
    .filter(observation => observation.elements.length > 0);
}

/**
 * Creates or updates the rule for `element` on this one patient's identifier (`identifierIndex`)
 * and records that the patient contributed it. Scoped by (patientId, identifierIndex, element), so
 * the same element on a different identifier — of this patient or another — keeps its own rule
 * rather than sharing this one.
 */
export function upsertPatientRule(
  rules: readonly MrnIdentifierRule[],
  observations: readonly MrnObservation[],
  patientId: string,
  identifierIndex: number,
  element: MrnRuleElement,
  operator: MrnRuleOperator,
  value: string
): {rules: MrnIdentifierRule[]; observations: MrnObservation[]} {
  const existingIndex = rules.findIndex(
    rule => rule.patientId === patientId && rule.identifierIndex === identifierIndex && rule.element === element
  );
  const nextRules = [...rules];
  if (existingIndex >= 0) {
    nextRules[existingIndex] = {...nextRules[existingIndex], rule: operator, value};
  } else {
    nextRules.push({ordinal: nextRules.length, element, rule: operator, value, patientId, identifierIndex});
  }

  const nextObservations = observations.map(observation => ({...observation}));
  const observationIndex = nextObservations.findIndex(o => o.patientId === patientId);
  if (observationIndex >= 0) {
    const current = nextObservations[observationIndex];
    if (!current.elements.includes(element)) {
      current.elements = [...current.elements, element];
    }
  } else {
    nextObservations.push({patientId, elements: [element]});
  }

  return {rules: withOrdinals(nextRules), observations: nextObservations};
}

/** Removes the rule for `element` on this one patient's identifier, and prunes the element out of
 * that patient's observations once nothing else of theirs still targets it. */
export function removeRuleByElement(
  rules: readonly MrnIdentifierRule[],
  observations: readonly MrnObservation[],
  patientId: string,
  identifierIndex: number,
  element: MrnRuleElement
): {rules: MrnIdentifierRule[]; observations: MrnObservation[]} {
  const nextRules = withOrdinals(
    rules.filter(
      rule => !(rule.patientId === patientId && rule.identifierIndex === identifierIndex && rule.element === element)
    )
  );
  return {rules: nextRules, observations: pruneObservations(nextRules, observations)};
}
