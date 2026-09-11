import React, {useEffect, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {AvailableMeasure} from '../../../api/contracts';
import {
  Button,
  ChipMultiSelect,
  DateField,
  NHSNLoadingIndicator,
  PageHeader,
  StepActions
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
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

  const [loading, setLoading] = useState(true);
  const [availableMeasures, setAvailableMeasures] = useState<AvailableMeasure[]>([]);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [requesting, setRequesting] = useState(false);
  const [advanceAfterRequest, setAdvanceAfterRequest] = useState(false);

  // Fetched fresh here rather than shared with the Reporting Plan step's copy -- simplest, and
  // avoids either step going stale relative to the other.
  useEffect(() => {
    let mounted = true;
    setLoading(true);

    api
      .getAvailableMeasures()
      .then(measures => {
        if (mounted) {
          setAvailableMeasures(measures);
        }
      })
      .catch(cause => {
        if (mounted) {
          notifyError(cause instanceof Error ? cause.message : t('onboarding:report.messages.measuresLoadError'));
        }
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

  // Advancing is deferred a render so the reducer's patch is in the draft the
  // provider persists — `onNext` saves the draft it was rendered with.
  useEffect(() => {
    if (advanceAfterRequest && report.lastRequestedReportId) {
      setAdvanceAfterRequest(false);
      onNext();
    }
  }, [advanceAfterRequest, report.lastRequestedReportId, onNext]);

  function clearFieldError(field: string) {
    setErrors(previous => {
      if (!previous[field]) {
        return previous;
      }
      const next = {...previous};
      delete next[field];
      return next;
    });
  }

  async function handleGenerate() {
    const nextErrors = validateReport(draft);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
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

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="report-generate">
      <div className="card">
        <div className="card-scroll">
          <PageHeader title={t('onboarding:report.title')} />
          <p className="subtitle">{t('onboarding:report.subtitle')}</p>

          <ChipMultiSelect
            id="reportMeasures"
            label={t('onboarding:report.fields.measuresLabel')}
            hint={t('onboarding:report.fields.measuresTooltip')}
            options={measureOptions}
            value={selectedMeasures}
            emptyText={t('onboarding:report.fields.measuresEmpty')}
            placeholder={t('onboarding:report.fields.measuresPlaceholder')}
            selectedLabel={t('onboarding:report.fields.measuresSelected')}
            removeLabel={label => t('onboarding:report.fields.measuresRemove', {measure: label})}
            error={errors.measures ? t(errors.measures) : undefined}
            onChange={selected => {
              patch('report', {measures: selected});
              clearFieldError('measures');
            }} />

          <div className="triplet">
            <DateField
              id="reportStartDate"
              label={t('onboarding:report.fields.startDateLabel')}
              required
              value={report.startDate}
              error={errors.startDate ? t(errors.startDate) : undefined}
              onChange={value => {
                patch('report', {startDate: value});
                clearFieldError('startDate');
              }} />
            <DateField
              id="reportEndDate"
              label={t('onboarding:report.fields.endDateLabel')}
              required
              value={report.endDate}
              error={errors.endDate ? t(errors.endDate) : undefined}
              onChange={value => {
                patch('report', {endDate: value});
                clearFieldError('endDate');
              }} />
          </div>

          <PatientSelection
            patientIds={report.patientIds ?? []}
            disabled={requesting}
            error={errors.patientIds ? t(errors.patientIds) : undefined}
            onChange={next => {
              patch('report', {patientIds: next});
              clearFieldError('patientIds');
            }} />
        </div>

        <StepActions saving={saving}>
          <Button variant="secondary" onClick={onBack} disabled={saving || requesting}>
            {t('common:actions.back')}
          </Button>
          <Button
            onClick={handleGenerate}
            disabled={saving || requesting}
            loading={requesting}>
            {t('onboarding:report.actions.generate')}
          </Button>
        </StepActions>
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
