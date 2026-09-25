import React, {useEffect, useMemo, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {AvailableMeasure} from '../../../api/contracts';
import {
  acronymTitle,
  Button,
  ChipMultiSelect,
  DateField,
  HeadingPause,
  NHSNLoadingIndicator,
  StepActions
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding, useStepValidator} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import {PatientSelection} from './PatientSelection';
import {enteredPatientIds, validateReport, type FieldErrors} from './validate';
import './ReportStep.css';

/** Generate Test Report: measures, a reporting period and the patients to run against. */
export function ReportStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError, notifySuccess} = useNotifications();
  const {draft, patch, saving} = useOnboarding();
  const report = draft.report;

  const [touched, setTouched] = useState<Record<string, boolean>>({});
  const [validationError, setValidationError] = useState<string | null>(null);
  const [requesting, setRequesting] = useState(false);
  const [advanceAfterRequest, setAdvanceAfterRequest] = useState(false);

  // Shares its cache with the Reporting Plan step via the query key -- both read the same
  // value, so neither can go stale relative to the other.
  const {
    data: availableMeasures = [],
    isLoading: loading,
    error: measuresError
  } = useQuery({
    queryKey: ['availableMeasures'],
    queryFn: () => api.getAvailableMeasures(),
    staleTime: Infinity
  });

  useEffect(() => {
    if (measuresError) {
      notifyError(measuresError instanceof Error ? measuresError.message : t('onboarding:report.messages.measuresLoadError'));
    }
  }, [measuresError]);

  // Advancing is deferred a render so the reducer's patch is in the draft the
  // provider persists — `onNext` saves the draft it was rendered with.
  useEffect(() => {
    if (advanceAfterRequest && report.lastRequestedReportId) {
      setAdvanceAfterRequest(false);
      onNext();
    }
  }, [advanceAfterRequest, report.lastRequestedReportId, onNext]);

  function markTouched(field: string) {
    setTouched(previous => (previous[field] ? previous : {...previous, [field]: true}));
  }

  function draftForValidation() {
    return {...draft, report: {...draft.report, measures: selectedMeasures}};
  }

  // True when the sole problem is a duplicate patient id and every other field is
  // complete. Distinguishes "you left something blank" from "everything's filled in,
  // but two rows match" so the two cases never announce contradictory messages at once.
  function isOnlyPatientIdsDuplicate(errs: FieldErrors): boolean {
    const keys = Object.keys(errs);
    return keys.length === 1 && errs.patientIds === 'onboarding:report.errors.patientIdsDuplicate';
  }

  function fieldError(field: string): string | undefined {
    if (!touched[field] || !errors[field]) {
      return undefined;
    }
    // The duplicate-id message is rendered once, inside PatientSelection. Outside the
    // duplicate-only case, either patientIds is missing entirely (the step banner's
    // "complete all fields" already says so) or another field is also incomplete (which
    // takes priority) -- either way, showing it here too would stack a second message
    // about the same row.
    if (field === 'patientIds' && !isOnlyPatientIdsDuplicate(errors)) {
      return undefined;
    }
    return t(errors[field]);
  }

  function announceValidationMessage(message: string) {
    setValidationError(null);
    window.setTimeout(() => setValidationError(message), 0);
  }

  function validateStep(): boolean {
    setTouched({measures: true, startDate: true, endDate: true, patientIds: true});
    const nextErrors = validateReport(draftForValidation());
    if (Object.keys(nextErrors).length === 0) {
      setValidationError(null);
      return true;
    }
    // A duplicate id with everything else complete gets its own message from
    // PatientSelection -- the generic banner would be both redundant and wrong (nothing
    // is actually incomplete).
    if (isOnlyPatientIdsDuplicate(nextErrors)) {
      setValidationError(null);
    } else {
      announceValidationMessage(t('onboarding:report.messages.incomplete'));
    }
    return false;
  }

  async function handleGenerate() {
    if (!validateStep()) {
      return;
    }

    setRequesting(true);
    try {
      const operation = await api.requestReport({
        // Resolve the selected NHSN measure names to the dQMs Tenant/MeasureEval know about.
        // Deduplicated -- two NHSN measures can map to the same dQM, and Tenant refuses a
        // request naming one twice.
        measures: resolveDigitalQualityMeasures(selectedMeasures, availableMeasures),
        startDate: report.startDate!,
        endDate: report.endDate!,
        // Blank rows are an editing state, not patients.
        patientIds: enteredPatientIds(draft)
      });
      // Resolves as soon as the request is accepted: the id is assigned
      // synchronously and the report is generated behind it, which is what the
      // Report Results step watches. Nothing here waits for a finished report.
      const summary = await operation.result();

      patch('report', {lastRequestedReportId: summary.reportId});
      // Report only stores the resolved dQM -- several placeholders can share one -- so the
      // original NHSN measure selection is kept here for Report Results/Details to display.
      patch('reportResults', {
        requestedMeasuresByReportId: {
          ...draft.reportResults.requestedMeasuresByReportId,
          [summary.reportId]: selectedMeasures
        }
      });
      notifySuccess(t('onboarding:report.messages.requested', {reportId: summary.reportId}));
      setAdvanceAfterRequest(true);
    } catch (cause) {
      notifyError(
        cause instanceof Error ? cause.message : t('onboarding:report.messages.requestError')
      );
    } finally {
      setRequesting(false);
    }
  }


  // The measure name is real facility data, not UI copy, so it's used as-is rather than
  // interpolated through t() -- i18next HTML-escapes interpolated values, which would mangle
  // "&"/"/" in a name like "Antimicrobial Use and Resistance (AU/AR)".
  const measureOptions = availableMeasures.map(measure => ({
    value: measure.name,
    label: measure.name
  }));

  // A draft saved against an earlier available-measures fetch can carry a name no longer
  // offered (the facility's plan or MeasureEval's definitions changed since). ChipMultiSelect
  // renders an unrecognized value as its own chip rather than dropping it, so it must be
  // filtered out here instead.
  const validMeasureNames = new Set(availableMeasures.map(measure => measure.name));
  const selectedMeasures = (report.measures ?? []).filter(name => validMeasureNames.has(name));

  // Same as FhirStep: errors are checked against the live draft, and only shown once a field is
  // touched (blurred, or flagged by Generate Report) - so fixing a field clears its error
  // immediately, and re-breaking it (e.g. moving the end date back before the start) shows it
  // again, without waiting for another blur or click.
  const errors = validateReport(draftForValidation());
  const incompleteMessage = t('onboarding:report.messages.incomplete');
  const stepIncomplete = Object.keys(errors).length > 0 && !isOnlyPatientIdsDuplicate(errors);

  // Once the "incomplete" banner is shown, it follows the fields live - fixing every one of them
  // clears it without another click, same as FhirStep. A leftover duplicate id alone doesn't hold
  // it up: that has its own message (see validateStep).
  useEffect(() => {
    if (validationError === incompleteMessage && !stepIncomplete) {
      setValidationError(null);
    }
  }, [validationError, incompleteMessage, stepIncomplete]);

  useStepValidator(validateStep);

  const stableOnBack = useStableCallback(onBack);
  const stableHandleGenerate = useStableCallback(handleGenerate);

  useStepChrome(
    useMemo(
      () =>
        loading
          ? null
          : {
              title: acronymTitle(<HeadingPause>{t('onboarding:report.title')}</HeadingPause>),
              footer: (
                <StepActions saving={saving}>
                  <Button variant="secondary" onClick={stableOnBack} disabled={saving || requesting}>
                    {t('common:actions.back')}
                  </Button>
                  <Button
                    onClick={stableHandleGenerate}
                    disabled={saving || requesting}
                    loading={requesting}>
                    {t('onboarding:report.actions.generate')}
                  </Button>
                </StepActions>
              )
            },
      [t, loading, saving, requesting, stableOnBack, stableHandleGenerate]
    )
  );

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="report-generate">
          <p className="subtitle">{t('onboarding:report.subtitle')}</p>

          <ChipMultiSelect
            id="reportMeasures"
            label={t('onboarding:report.fields.measuresLabel')}
            hint={t('onboarding:report.fields.measuresTooltip')}
            required
            options={measureOptions}
            value={selectedMeasures}
            emptyText={t('onboarding:report.fields.measuresEmpty')}
            placeholder={t('onboarding:report.fields.measuresPlaceholder')}
            selectedLabel={t('onboarding:report.fields.measuresSelected')}
            removeLabel={label => t('onboarding:report.fields.measuresRemove', {measure: label})}
            error={fieldError('measures')}
            onChange={selected => patch('report', {measures: selected})}
            onBlur={() => markTouched('measures')} />

          <div className="triplet">
            <DateField
              id="reportStartDate"
              label={t('onboarding:report.fields.startDateLabel')}
              required
              value={report.startDate}
              error={fieldError('startDate')}
              onChange={value => patch('report', {startDate: value})}
              onBlur={() => markTouched('startDate')} />
            <DateField
              id="reportEndDate"
              label={t('onboarding:report.fields.endDateLabel')}
              required
              value={report.endDate}
              error={fieldError('endDate')}
              onChange={value => patch('report', {endDate: value})}
              onBlur={() => markTouched('endDate')} />
          </div>

          <PatientSelection
            patientIds={report.patientIds ?? []}
            disabled={requesting}
            error={fieldError('patientIds')}
            showEmptyRowErrors={touched.patientIds}
            onChange={next => patch('report', {patientIds: next})} />

          <div aria-live="off">
            <p className="nhsn-link__form-error" role="alert">
              {validationError}
            </p>
          </div>
    </div>
  );
}

/**
 * Selected NHSN measure names to the dQMs a report request carries. Deduplicated because two
 * NHSN measures can share one dQM -- Tenant refuses a request that names one twice.
 */
function resolveDigitalQualityMeasures(
  selectedNames: readonly string[],
  availableMeasures: readonly AvailableMeasure[]
): string[] {
  const dqmByName = new Map(availableMeasures.map(measure => [measure.name, measure.digitalQualityMeasure]));
  return [...new Set(selectedNames.map(name => dqmByName.get(name)).filter((dqm): dqm is string => Boolean(dqm)))];
}

export default ReportStep;
