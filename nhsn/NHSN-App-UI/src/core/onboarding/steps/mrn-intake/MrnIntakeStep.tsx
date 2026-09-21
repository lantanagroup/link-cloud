import React, {useEffect, useMemo, useState} from 'react';
import {Trans, useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {MrnIdentifierRule, MrnIntake, MrnIntakeOptions, PatientIdentifier} from '../../../api/contracts';
import {
  AcronymText,
  acronymLabel,
  acronymTitle,
  Button,
  CheckboxField,
  FieldLabel,
  HeadingPause,
  MessageContainer,
  Modal,
  NHSNLoadingIndicator,
  RepeatableList,
  Select,
  StepActions,
  TableCaption,
  TextField,
  YesNoField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import {
  findRuleForElement,
  identifierElementValue,
  IDENTIFIER_FIELD_LABEL_KEYS,
  MRN_RULE_ELEMENTS,
  operatorNeedsValue,
  operatorsForElement,
  placeholderKeyForElement,
  removeRuleByElement,
  ruleCountForPatient,
  upsertPatientRule,
  withOrdinals,
  pruneObservations,
  type MrnRuleElement,
  type MrnRuleOperator
} from './rules';
import {defaultMrnIntake, toMrnIntake, validateMrnIntake, type MrnIntakeDraft} from './validate';
import './MrnIntakeStep.css';

// Shown only for the instant before api.getMrnIntakeOptions() resolves.
const EMPTY_OPTIONS: MrnIntakeOptions = {multipleMrnTypes: [], varianceTypes: [], changeTypes: []};

/**
 * MRN Identifier Intake — the flow's last data-entry step. Unlike every other step, its data is
 * normalized server-side: the draft only ever holds a read-only mirror (`draft.mrnIntake`,
 * written by the `'mrn/mirror'` reducer action), and this component reads/writes through
 * `getMrnIntake`/`saveMrnIntake` directly rather than `patch()`. Its forward action saves, then
 * completes the whole enrollment (`completeOnboarding`) — there is no plain "Continue".
 */
export function MrnIntakeStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {dispatch, saving, savingDirection} = useOnboarding();
  const {notifyError} = useNotifications();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [draft, setDraft] = useState<MrnIntakeDraft>(defaultMrnIntake());
  const [patients, setPatients] = useState<PatientIdentifier[]>([]);
  const [options, setOptions] = useState<MrnIntakeOptions>(EMPTY_OPTIONS);
  const [selectedPatientId, setSelectedPatientId] = useState<string | null>(null);
  const [editingTarget, setEditingTarget] = useState<EditingTarget | null>(null);
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    Promise.all([api.getMrnIntake(), api.getPatientIdentifiers(), api.getMrnIntakeOptions()])
      .then(([intake, identifiers, mrnIntakeOptions]) => {
        if (!mounted) {
          return;
        }
        setDraft(intake ?? defaultMrnIntake());
        setPatients(identifiers);
        setOptions(mrnIntakeOptions);
      })
      .catch(cause => {
        if (!mounted) {
          return;
        }
        setLoadError(cause instanceof Error ? cause.message : t('onboarding:mrnIntake.messages.loadError'));
      })
      .finally(() => {
        if (mounted) {
          setLoading(false);
        }
      });
    return () => {
      mounted = false;
    };
  }, [api]);

  useEffect(() => {
    setEditingTarget(null);
  }, [selectedPatientId]);

  const errors = useMemo(() => (submitAttempted ? validateMrnIntake(draft) : {}), [submitAttempted, draft]);

  // Question 2's options mirror whichever identifier types were checked in Question 1, using the
  // free-text label for "Other" — matches the POC's getMrnUserFacingOptions (index.html:5171-5178).
  const userFacingOptions = useMemo(
    () =>
      options.multipleMrnTypes.filter(option => draft.multipleMrnTypes.includes(option.value)).map(option => ({
        value: option.value,
        label:
          option.value === 'other'
            ? draft.multipleMrnOtherText?.trim() || t(option.labelKey)
            : t(option.labelKey)
      })),
    [options.multipleMrnTypes, draft.multipleMrnTypes, draft.multipleMrnOtherText, t]
  );

  // A type unchecked in Question 1 (or a since-renamed "Other" label) can't stay selected here.
  useEffect(() => {
    setDraft(prev => {
      const allowed = new Set<string>(userFacingOptions.map(option => option.value));
      const filtered = prev.userFacingIdentifierNames.filter(value => allowed.has(value));
      return filtered.length === prev.userFacingIdentifierNames.length ? prev : {...prev, userFacingIdentifierNames: filtered};
    });
  }, [draft.multipleMrnTypes, draft.multipleMrnOtherText]);

  function update(patch: Partial<MrnIntakeDraft>) {
    setDraft(prev => ({...prev, ...patch}));
  }

  function handleRulesChange(nextRules: MrnIdentifierRule[]) {
    const ordered = withOrdinals(nextRules);
    setDraft(prev => ({...prev, rules: ordered, observations: pruneObservations(ordered, prev.observations)}));
  }

  function applyPatientRule(
    patientId: string,
    identifierIndex: number,
    element: MrnRuleElement,
    operator: MrnRuleOperator,
    value: string
  ) {
    const {rules, observations} = upsertPatientRule(
      draft.rules,
      draft.observations,
      patientId,
      identifierIndex,
      element,
      operator,
      value
    );
    setDraft(prev => ({...prev, rules, observations}));
    setEditingTarget(null);
  }

  function removePatientRule(patientId: string, identifierIndex: number, element: MrnRuleElement) {
    const {rules, observations} = removeRuleByElement(draft.rules, draft.observations, patientId, identifierIndex, element);
    setDraft(prev => ({...prev, rules, observations}));
  }

  async function handleComplete() {
    setSubmitAttempted(true);
    const validationErrors = validateMrnIntake(draft);
    if (Object.keys(validationErrors).length > 0) {
      notifyError(t('onboarding:mrnIntake.messages.incomplete'));
      return;
    }

    const intake: MrnIntake = toMrnIntake(draft);
    setSubmitting(true);
    setSubmitError(null);

    try {
      await api.saveMrnIntake(intake);
    } catch (cause) {
      setSubmitting(false);
      setSubmitError(cause instanceof Error ? cause.message : t('onboarding:mrnIntake.messages.saveError'));
      return;
    }

    try {
      await api.completeOnboarding();
    } catch (cause) {
      setSubmitting(false);
      setSubmitError(cause instanceof Error ? cause.message : t('onboarding:mrnIntake.messages.completeError'));
      return;
    }

    dispatch({type: 'mrn/mirror', intake});
    setSubmitting(false);
    onNext();
  }

  const busy = saving || submitting;
  const stableOnBack = useStableCallback(onBack);
  const stableHandleComplete = useStableCallback(handleComplete);

  useStepChrome(
    useMemo(
      () =>
        loading
          ? null
          : {
              title: acronymTitle(<HeadingPause>{t('onboarding:mrnIntake.title')}</HeadingPause>),
              footer: (
                <StepActions saving={busy}>
                  <Button variant="secondary" onClick={stableOnBack} disabled={busy} loading={savingDirection === 'back'}>
                    {t('common:actions.back')}
                  </Button>
                  <Button onClick={stableHandleComplete} disabled={busy} loading={submitting || savingDirection === 'next'}>
                    {t('onboarding:mrnIntake.actions.complete')}
                  </Button>
                </StepActions>
              )
            },
      [t, loading, busy, savingDirection, submitting, stableOnBack, stableHandleComplete]
    )
  );

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  const selectedPatient = patients.find(patient => patient.patientId === selectedPatientId) ?? null;

  return (
    <div className="nhsn-link__mrn-intake">
      <p className="nhsn-link__subtitle"><AcronymText>{t('onboarding:mrnIntake.intro1')}</AcronymText></p>
      <p className="nhsn-link__subtitle">
        <Trans t={t} i18nKey="onboarding:mrnIntake.intro2" components={{b: <b />}} />
      </p>

      {loadError && (
        <MessageContainer type="error" showIcon>
          <span role="alert">{loadError}</span>
        </MessageContainer>
      )}

      {/* ---------------------------------------------------------- multiple MRN candidates */}
      <div className="nhsn-link__section-title">{t('onboarding:mrnIntake.sections.multipleMrn')}</div>
      <div className="nhsn-link__field-group">
        <YesNoField
          label={t('onboarding:mrnIntake.multipleMrn.question')}
          yesLabel={t('common:commonBoolean.yes')}
          noLabel={t('common:commonBoolean.no')}
          value={draft.hasMultipleMrn}
          onChange={hasMultipleMrn => update({hasMultipleMrn})}
          error={errors.hasMultipleMrn && t(errors.hasMultipleMrn)}
        />
      </div>

      {draft.hasMultipleMrn === true && (
        <div className="nhsn-link__field-group">
          <FieldLabel checked={false}>{t('onboarding:mrnIntake.multipleMrn.selectAllHint')}</FieldLabel>
          <CheckboxGroup<string>
            idPrefix="mrn-multiple-type"
            groupLabel={t('onboarding:mrnIntake.multipleMrn.selectAllHint')}
            options={options.multipleMrnTypes.map(option => ({value: option.value, label: t(option.labelKey)}))}
            values={draft.multipleMrnTypes}
            onChange={multipleMrnTypes => update({multipleMrnTypes})}
          />
          {errors.multipleMrnTypes && (
            <p className="nhsn-link__form-error" role="alert">
              {t(errors.multipleMrnTypes)}
            </p>
          )}
          {draft.multipleMrnTypes.includes('other') && (
            <TextField
              id="mrn-multiple-type-other-text"
              label={t('onboarding:mrnIntake.multipleMrn.otherLabel')}
              value={draft.multipleMrnOtherText ?? ''}
              error={errors.multipleMrnOtherText && t(errors.multipleMrnOtherText)}
              onChange={multipleMrnOtherText => update({multipleMrnOtherText})}
            />
          )}
        </div>
      )}

      {/* ---------------------------------------------------------- user-facing MRN */}
      {draft.hasMultipleMrn === true && (
        <>
          <div className="nhsn-link__section-title"><AcronymText>{t('onboarding:mrnIntake.sections.userFacingMrn')}</AcronymText></div>
          <div className="nhsn-link__field-group">
            <FieldLabel checked={false}><AcronymText>{t('onboarding:mrnIntake.userFacing.question')}</AcronymText></FieldLabel>
            {userFacingOptions.length === 0 ? (
              <p className="nhsn-link__hint-text">{t('onboarding:mrnIntake.userFacing.noOptionsHint')}</p>
            ) : (
              <CheckboxGroup<string>
                idPrefix="mrn-user-facing"
                groupLabel={acronymLabel(t('onboarding:mrnIntake.userFacing.question'))}
                options={userFacingOptions}
                values={draft.userFacingIdentifierNames}
                onChange={userFacingIdentifierNames => update({userFacingIdentifierNames})}
              />
            )}
            {errors.userFacingIdentifierNames && (
              <p className="nhsn-link__form-error" role="alert">
                {t(errors.userFacingIdentifierNames)}
              </p>
            )}
          </div>

          <div className="nhsn-link__field-group">
            <YesNoField
              label={t('onboarding:mrnIntake.userFacing.isCalledMrnQuestion')}
              yesLabel={t('common:commonBoolean.yes')}
              noLabel={t('common:commonBoolean.no')}
              value={draft.isCalledMrn}
              onChange={isCalledMrn => update({isCalledMrn})}
              error={errors.isCalledMrn && t(errors.isCalledMrn)}
            />
          </div>

          {draft.isCalledMrn === false && (
            <div className="nhsn-link__field-group">
              <TextField
                id="mrn-other-term"
                label={t('onboarding:mrnIntake.userFacing.otherTermLabel')}
                value={draft.otherTermUsed ?? ''}
                error={errors.otherTermUsed && t(errors.otherTermUsed)}
                onChange={otherTermUsed => update({otherTermUsed})}
              />
            </div>
          )}

          <div className="nhsn-link__field-group">
            <YesNoField
              label={t('onboarding:mrnIntake.userFacing.canSearchQuestion')}
              yesLabel={t('common:commonBoolean.yes')}
              noLabel={t('common:commonBoolean.no')}
              value={draft.canSearchByIdentifier}
              onChange={canSearchByIdentifier => update({canSearchByIdentifier})}
              error={errors.canSearchByIdentifier && t(errors.canSearchByIdentifier)}
            />
          </div>
        </>
      )}

      {/* ---------------------------------------------------------- corresponding FHIR identifier */}
      <div className="nhsn-link__section-title">{t('onboarding:mrnIntake.sections.correspondingIdentifier')}</div>
      <p className="nhsn-link__hint-text">{t('onboarding:mrnIntake.identifierTable.hint')}</p>

      <div className="nhsn-link__table-scroll nhsn-link__mrn-patient-table-scroll" tabIndex={-1}>
        <table className="nhsn-link__table nhsn-link__mrn-patient-table">
          <TableCaption>{t('onboarding:mrnIntake.sections.correspondingIdentifier')}</TableCaption>
          <thead>
            <tr>
              <th scope="col">{t('onboarding:mrnIntake.identifierTable.columns.patientId')}</th>
              <th scope="col">{t('onboarding:mrnIntake.identifierTable.columns.identifiers')}</th>
              <th scope="col">{t('onboarding:mrnIntake.identifierTable.columns.rulesAdded')}</th>
            </tr>
          </thead>
          <tbody>
            {patients.map(patient => (
              <tr key={patient.patientId}>
                <td>
                  <button
                    type="button"
                    className="nhsn-link__mrn-patient-button"
                    onClick={() => setSelectedPatientId(patient.patientId)}>
                    {patient.patientId}
                  </button>
                </td>
                <td>{patient.elements.length}</td>
                <td>{ruleCountForPatient(draft.rules, draft.observations, patient.patientId)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {patients.length === 0 && (
        <p className="nhsn-link__hint-text">{t('onboarding:mrnIntake.identifierTable.empty')}</p>
      )}

      <Modal
        open={selectedPatient !== null}
        title={selectedPatient?.patientId ?? ''}
        size="large"
        onClose={() => setSelectedPatientId(null)}
        footer={
          <Button variant="secondary" onClick={() => setSelectedPatientId(null)}>
            {t('common:actions.close')}
          </Button>
        }>
        {selectedPatient && (
          <PatientIdentifierDetail
            patient={selectedPatient}
            rules={draft.rules}
            editingTarget={editingTarget}
            onOpenEditor={setEditingTarget}
            onCloseEditor={() => setEditingTarget(null)}
            onSaveRule={(identifierIndex, element, operator, value) =>
              applyPatientRule(selectedPatient.patientId, identifierIndex, element, operator, value)
            }
            onRemoveRule={(identifierIndex, element) => removePatientRule(selectedPatient.patientId, identifierIndex, element)}
          />
        )}
      </Modal>

      {/* ---------------------------------------------------------- rule builder */}
      <div className="nhsn-link__section-title">{t('onboarding:mrnIntake.sections.ruleToIdentify')}</div>
      <p className="nhsn-link__hint-text">{t('onboarding:mrnIntake.rules.hint')}</p>
      <RepeatableList<MrnIdentifierRule>
        items={draft.rules}
        onChange={handleRulesChange}
        newItem={() => ({ordinal: draft.rules.length, element: 'system', rule: 'equals', value: ''})}
        addLabel={t('onboarding:mrnIntake.rules.addButton')}
        removeLabel={t('common:actions.remove')}
        emptyLabel={t('onboarding:mrnIntake.rules.emptyState')}
        renderItem={(row, index, onRowChange) => {
          const element = row.element as MrnRuleElement;
          const operators = operatorsForElement(element);
          const needsValue = operatorNeedsValue(element, row.rule);
          return (
            <>
              <Select<MrnRuleElement>
                id={`mrn-rule-element-${index}`}
                label={t('onboarding:mrnIntake.rules.elementLabel')}
                options={MRN_RULE_ELEMENTS.map(def => ({value: def.key, label: t(def.labelKey)}))}
                value={element}
                onChange={nextElement => {
                  const nextOperators = operatorsForElement(nextElement);
                  onRowChange({
                    ...row,
                    element: nextElement,
                    rule: nextOperators[0].key,
                    value: nextOperators[0].needsValue ? row.value : ''
                  });
                }}
              />
              <Select<MrnRuleOperator>
                id={`mrn-rule-operator-${index}`}
                label={t('onboarding:mrnIntake.rules.operatorLabel')}
                options={operators.map(operator => ({value: operator.key, label: t(operator.labelKey)}))}
                value={row.rule as MrnRuleOperator}
                onChange={nextOperator =>
                  onRowChange({...row, rule: nextOperator, value: operatorNeedsValue(element, nextOperator) ? row.value : ''})
                }
              />
              {needsValue && (
                <TextField
                  id={`mrn-rule-value-${index}`}
                  label={t('onboarding:mrnIntake.rules.valueLabel')}
                  placeholder={t(placeholderKeyForElement(element))}
                  value={row.value}
                  onChange={value => onRowChange({...row, value})}
                />
              )}
            </>
          );
        }}
      />
      {errors.rules && (
        <p className="nhsn-link__form-error" role="alert">
          {t(errors.rules)}
        </p>
      )}
      {errors.ruleValues && (
        <p className="nhsn-link__form-error" role="alert">
          {t(errors.ruleValues)}
        </p>
      )}

      {/* ---------------------------------------------------------- variance across facilities */}
      <div className="nhsn-link__section-title">{t('onboarding:mrnIntake.sections.variability')}</div>
      <div className="nhsn-link__field-group">
        <YesNoField
          label={t('onboarding:mrnIntake.variance.question')}
          yesLabel={t('common:commonBoolean.yes')}
          noLabel={t('common:commonBoolean.no')}
          value={draft.variesByFacility}
          onChange={variesByFacility => update({variesByFacility})}
          error={errors.variesByFacility && t(errors.variesByFacility)}
        />
      </div>
      {draft.variesByFacility === true && (
        <div className="nhsn-link__field-group">
          <FieldLabel checked={false}>{t('onboarding:mrnIntake.variance.selectAllHint')}</FieldLabel>
          <CheckboxGroup<string>
            idPrefix="mrn-variance-type"
            groupLabel={t('onboarding:mrnIntake.variance.selectAllHint')}
            options={options.varianceTypes.map(option => ({value: option.value, label: t(option.labelKey)}))}
            values={draft.varianceTypes}
            onChange={varianceTypes => update({varianceTypes})}
          />
          {errors.varianceTypes && (
            <p className="nhsn-link__form-error" role="alert">
              {t(errors.varianceTypes)}
            </p>
          )}
          {draft.varianceTypes.includes('other') && (
            <TextField
              id="mrn-variance-other-text"
              label={t('onboarding:mrnIntake.variance.otherLabel')}
              value={draft.varianceOtherText ?? ''}
              error={errors.varianceOtherText && t(errors.varianceOtherText)}
              onChange={varianceOtherText => update({varianceOtherText})}
            />
          )}
        </div>
      )}

      {/* ---------------------------------------------------------- MRN changes over time */}
      <div className="nhsn-link__section-title">{t('onboarding:mrnIntake.sections.changesOverTime')}</div>
      <div className="nhsn-link__field-group">
        <YesNoField
          label={t('onboarding:mrnIntake.changes.question')}
          yesLabel={t('common:commonBoolean.yes')}
          noLabel={t('common:commonBoolean.no')}
          value={draft.changesOverTime}
          onChange={changesOverTime => update({changesOverTime})}
          error={errors.changesOverTime && t(errors.changesOverTime)}
        />
      </div>
      {draft.changesOverTime === true && (
        <div className="nhsn-link__field-group">
          <FieldLabel checked={false}>{t('onboarding:mrnIntake.changes.selectAllHint')}</FieldLabel>
          <CheckboxGroup<string>
            idPrefix="mrn-change-type"
            groupLabel={t('onboarding:mrnIntake.changes.selectAllHint')}
            options={options.changeTypes.map(option => ({value: option.value, label: t(option.labelKey)}))}
            values={draft.changeTypes}
            onChange={changeTypes => update({changeTypes})}
          />
          {errors.changeTypes && (
            <p className="nhsn-link__form-error" role="alert">
              {t(errors.changeTypes)}
            </p>
          )}
          {draft.changeTypes.includes('other') && (
            <TextField
              id="mrn-change-other-text"
              label={t('onboarding:mrnIntake.changes.otherLabel')}
              value={draft.changeOtherText ?? ''}
              error={errors.changeOtherText && t(errors.changeOtherText)}
              onChange={changeOtherText => update({changeOtherText})}
            />
          )}
        </div>
      )}

      {submitError && (
        <MessageContainer type="error" showIcon>
          <span role="alert">{submitError}</span>
        </MessageContainer>
      )}
    </div>
  );
}

export default MrnIntakeStep;

// ---------------------------------------------------------------- checkbox group

interface CheckboxGroupOption<T extends string> {
  value: T;
  label: string;
}

function CheckboxGroup<T extends string>({
  idPrefix,
  groupLabel,
  options,
  values,
  onChange
}: {
  idPrefix: string;
  groupLabel: string;
  options: Array<CheckboxGroupOption<T>>;
  values: T[];
  onChange: (values: T[]) => void;
}) {
  return (
    <div className="nhsn-link__mrn-checkbox-group" role="group" aria-label={groupLabel}>
      {options.map(option => (
        <CheckboxField
          key={option.value}
          id={`${idPrefix}-${option.value}`}
          label={option.label}
          value={values.includes(option.value)}
          onChange={checked => onChange(checked ? [...values, option.value] : values.filter(value => value !== option.value))}
        />
      ))}
    </div>
  );
}

// ---------------------------------------------------------------- patient identifier detail

/** Which one (identifier card, element row) has its inline editor open — not element alone, since
 * a patient can have several identifiers and only the one actually clicked should open. */
interface EditingTarget {
  identifierIndex: number;
  element: MrnRuleElement;
}

interface PatientIdentifierDetailProps {
  patient: PatientIdentifier;
  rules: MrnIdentifierRule[];
  editingTarget: EditingTarget | null;
  onOpenEditor: (target: EditingTarget) => void;
  onCloseEditor: () => void;
  onSaveRule: (identifierIndex: number, element: MrnRuleElement, operator: MrnRuleOperator, value: string) => void;
  onRemoveRule: (identifierIndex: number, element: MrnRuleElement) => void;
}

function PatientIdentifierDetail({
  patient,
  rules,
  editingTarget,
  onOpenEditor,
  onCloseEditor,
  onSaveRule,
  onRemoveRule
}: PatientIdentifierDetailProps) {
  const {t} = useTranslation(['onboarding', 'common']);

  if (patient.elements.length === 0) {
    return <p className="nhsn-link__hint-text">{t('onboarding:mrnIntake.identifierTable.noIdentifiers')}</p>;
  }

  return (
    <>
      {patient.elements.map((identifier, index) => (
        <div className="nhsn-link__mrn-identifier-card" key={index}>
          <div className="nhsn-link__mrn-identifier-card-title">
            {t('onboarding:mrnIntake.identifierTable.identifierHeading', {number: index + 1})}
          </div>
          {MRN_RULE_ELEMENTS.map(def => {
            const value = identifierElementValue(def.key, identifier);
            const existingRule = findRuleForElement(rules, patient.patientId, index, def.key);

            return (
              <div className="nhsn-link__mrn-id-field" key={def.key}>
                <span className="nhsn-link__mrn-id-field-label">{t(IDENTIFIER_FIELD_LABEL_KEYS[def.key])}</span>
                <span
                  className={
                    value === undefined ? 'nhsn-link__mrn-id-field-value nhsn-link__mrn-na' : 'nhsn-link__mrn-id-field-value'
                  }>
                  {value ?? t('onboarding:mrnIntake.identifierTable.notAvailable')}
                </span>

                {value !== undefined &&
                  (editingTarget?.identifierIndex === index && editingTarget.element === def.key ? (
                    <PatientRuleEditor
                      element={def.key}
                      existingRule={existingRule}
                      onSave={(operator, ruleValue) => onSaveRule(index, def.key, operator, ruleValue)}
                      onCancel={onCloseEditor}
                    />
                  ) : existingRule ? (
                    <div className="nhsn-link__mrn-id-rule-action">
                      <span className="nhsn-link__mrn-id-rule-badge">
                        {operatorNeedsValue(def.key, existingRule.rule) && existingRule.value
                          ? t('onboarding:mrnIntake.identifierTable.ruleAddedWithValue', {
                              operator: t(operatorLabelKey(def.key, existingRule.rule)),
                              value: existingRule.value
                            })
                          : t('onboarding:mrnIntake.identifierTable.ruleAdded', {
                              operator: t(operatorLabelKey(def.key, existingRule.rule))
                            })}
                      </span>
                      <Button variant="secondary" size="sm" onClick={() => onOpenEditor({identifierIndex: index, element: def.key})}>
                        {t('common:actions.edit')}
                      </Button>
                      <Button variant="secondary" size="sm" onClick={() => onRemoveRule(index, def.key)}>
                        {t('common:actions.remove')}
                      </Button>
                    </div>
                  ) : (
                    <div className="nhsn-link__mrn-id-rule-action">
                      <Button variant="secondary" size="sm" onClick={() => onOpenEditor({identifierIndex: index, element: def.key})}>
                        {t('onboarding:mrnIntake.identifierTable.addRule')}
                      </Button>
                    </div>
                  ))}
              </div>
            );
          })}
        </div>
      ))}
    </>
  );
}

function operatorLabelKey(element: MrnRuleElement, operator: string): string {
  return operatorsForElement(element).find(def => def.key === operator)?.labelKey ?? operator;
}

interface PatientRuleEditorProps {
  element: MrnRuleElement;
  existingRule?: MrnIdentifierRule;
  onSave: (operator: MrnRuleOperator, value: string) => void;
  onCancel: () => void;
}

function PatientRuleEditor({element, existingRule, onSave, onCancel}: PatientRuleEditorProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const operators = operatorsForElement(element);
  const [operator, setOperator] = useState<MrnRuleOperator>(
    existingRule ? (existingRule.rule as MrnRuleOperator) : operators[0].key
  );
  const [value, setValue] = useState(existingRule?.value ?? '');
  const needsValue = operatorNeedsValue(element, operator);

  return (
    <div className="nhsn-link__mrn-id-rule-editor">
      <Select<MrnRuleOperator>
        id={`mrn-id-rule-operator-${element}`}
        label={t('onboarding:mrnIntake.rules.operatorLabel')}
        options={operators.map(def => ({value: def.key, label: t(def.labelKey)}))}
        value={operator}
        onChange={next => {
          setOperator(next);
          if (!operatorNeedsValue(element, next)) {
            setValue('');
          }
        }}
      />
      {needsValue && (
        <TextField
          id={`mrn-id-rule-value-${element}`}
          label={t('onboarding:mrnIntake.rules.valueLabel')}
          placeholder={t(placeholderKeyForElement(element))}
          value={value}
          onChange={setValue}
        />
      )}
      <Button size="sm" onClick={() => onSave(operator, needsValue ? value.trim() : '')}>
        {t('common:actions.save')}
      </Button>
      <Button variant="secondary" size="sm" onClick={onCancel}>
        {t('common:actions.cancel')}
      </Button>
    </div>
  );
}
