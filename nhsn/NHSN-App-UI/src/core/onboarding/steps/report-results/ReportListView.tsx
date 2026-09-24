import React, { useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useApiClient } from '../../../api/ApiClientContext';
import type { ReportSummary } from '../../../api/contracts';
import {
  acronymTitle,
  Button,
  CheckboxField,
  HeadingPause,
  MessageContainer,
  NHSNLoadingIndicator,
  StepActions,
  TableCaption,
} from '../../../fields';
import { useNotifications } from '../../../notifications/NotificationProvider';
import { useOnboarding } from '../../OnboardingProvider';
import { useStableCallback, useStepChrome } from '../../StepChrome';
import { formatDate, formatDateTime } from './format';
import { RefreshIcon } from './icons';
import { friendlyMeasuresFor, measureColor } from './reportMeasures';
import { STATUS_PILL_CLASS } from './reportStatus';

interface ReportListViewProps {
  onBack: () => void;
  onNext: () => void | Promise<void>;
  validationMessage: string | null;
  clearValidationMessage: () => void;
}

/**
 * The reports list sub-view: the generated-reports table, "Generate New Report", and the
 * report-accuracy acknowledgement checkbox. `validationMessage`/`clearValidationMessage` are
 * owned by ReportResultsStep, since the accuracy-acknowledgement gate on Continue must stay
 * registered with useStepValidator whether or not this sub-view is the one currently mounted.
 */
export function ReportListView({
  onBack,
  onNext,
  validationMessage,
  clearValidationMessage,
}: ReportListViewProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const { notifySuccess, notifyError } = useNotifications();
  const { draft, patch, saving, savingDirection, goTo, openView } = useOnboarding();
  const reportResults = draft.reportResults;
  const queryClient = useQueryClient();

  const {
    data: reportsPage,
    isLoading: loading,
    isFetching: reportsFetching,
    error: reportsQueryError,
    refetch: refetchReports,
  } = useQuery({
    queryKey: ['reports'],
    queryFn: () => api.listReports({ page: 1, pageSize: 50 }),
  });
  const reports = reportsPage?.items ?? [];
  const refreshing = reportsFetching && !loading;
  const loadError = reportsQueryError
    ? reportsQueryError instanceof Error
      ? reportsQueryError.message
      : t('onboarding:reportResults.messages.loadError')
    : null;

  // Scoped to the most recently generated report -- newest first, so reports[0]. Same query key
  // as ReportResultsStep's own copy of this query, so both share one cache entry: this component
  // owns the checkbox UI, ReportResultsStep's handleNext reads the same cached value to gate
  // Continue.
  const latestReportId = reports[0]?.reportId;
  const reportAcknowledgementQueryKey = [
    'reportAccuracyAcknowledgement',
    latestReportId,
  ] as const;
  const { data: reportAccuracyAcknowledged } = useQuery({
    queryKey: reportAcknowledgementQueryKey,
    queryFn: () => api.getReportAcknowledgement(latestReportId!),
    enabled: Boolean(latestReportId),
    staleTime: Infinity,
  });

  // Real, report-scoped acknowledgement (AcknowledgementKind.ReportAccuracy, contextId the report
  // id) -- the same append-only attestation mechanism the Census step already uses, just keyed by
  // report instead of facility. Deferred to Continue, matching the Census pattern: toggling the
  // checkbox updates the local query cache (what ReportResultsStep's validator and handleNext
  // read) and patches the draft (what marks the step dirty for the unsaved-changes prompt);
  // ReportResultsStep's handleNext is what actually PUTs the real acknowledgement.
  function handleAckChange(checked: boolean) {
    if (!latestReportId) {
      return;
    }
    queryClient.setQueryData(reportAcknowledgementQueryKey, checked);
    patch('reportResults', { accuracyAcknowledged: checked });
    clearValidationMessage();
  }

  async function handleRefresh() {
    const result = await refetchReports();
    if (result.isSuccess) {
      notifySuccess(t('onboarding:reportResults.messages.refreshed'));
    } else {
      notifyError(t('onboarding:reportResults.messages.loadError'));
    }
  }

  function handleGenerateNew() {
    goTo('report');
  }

  // The onboarding POC always makes the report id clickable, regardless of status -- a Pending or
  // Failed report still has whatever detail Report has recorded for it so far.
  function handleSelectReport(report: ReportSummary) {
    patch('reportResults', {
      viewingReportId: report.reportId,
      latestStatus: report.status,
    });
    openView({
      stepId: 'report-results',
      view: 'detail',
      params: { reportId: report.reportId },
    });
  }

  const stableOnBack = useStableCallback(onBack);
  const stableOnNext = useStableCallback(onNext);

  useStepChrome(
    useMemo(
      () => ({
        title: acronymTitle(<HeadingPause>{t('onboarding:reportResults.title')}</HeadingPause>),
        footer: (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={stableOnBack} disabled={saving} loading={savingDirection === 'back'}>
              {t('common:actions.back')}
            </Button>
            <Button onClick={stableOnNext} disabled={saving} loading={savingDirection === 'next'}>
              {t('common:actions.continue')}
            </Button>
          </StepActions>
        ),
      }),
      [t, saving, savingDirection, stableOnBack, stableOnNext],
    ),
  );

  return (
    <>
      <p className="nhsn-link__subtitle">
        {t('onboarding:reportResults.subtitle')}
      </p>

      <div className="nhsn-link__report-results-actions">
        <Button variant="secondary" onClick={onBack} disabled={saving}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={handleGenerateNew} disabled={saving || loading}>
          {t('onboarding:reportResults.actions.generateNewReport')}
        </Button>
        <Button
          variant="secondary"
          onClick={handleRefresh}
          disabled={saving || refreshing}>
          <RefreshIcon />
          {t('common:actions.refresh')}
        </Button>
      </div>

      <p className="nhsn-link__visually-hidden" role="alert">
        {!loading ? loadError : null}
      </p>

      {loading && <NHSNLoadingIndicator />}

      {!loading && loadError && reports.length === 0 && (
        <MessageContainer type="error" showIcon>
          <span>{loadError}</span>
        </MessageContainer>
      )}

      {!loading && reports.length > 0 && (
        <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
          <table className="nhsn-link__report-results-table nhsn-link__report-results-table--fixed">
            <TableCaption>{t('onboarding:reportResults.title')}</TableCaption>
            <colgroup>
              <col style={{ width: '11%' }} />
              <col style={{ width: '26%' }} />
              <col style={{ width: '13%' }} />
              <col style={{ width: '13%' }} />
              <col style={{ width: '13%' }} />
              <col style={{ width: '15%' }} />
              <col style={{ width: '9%' }} />
            </colgroup>
            <thead>
              <tr>
                <th scope="col">
                  {t('onboarding:reportResults.columns.reportId')}
                </th>
                <th scope="col">
                  {t('onboarding:reportResults.columns.measures')}
                </th>
                <th scope="col">
                  {t('onboarding:reportResults.columns.patientCount')}
                </th>
                <th scope="col">
                  {t('onboarding:reportResults.columns.startDate')}
                </th>
                <th scope="col">
                  {t('onboarding:reportResults.columns.endDate')}
                </th>
                <th scope="col">
                  {t('onboarding:reportResults.columns.createDate')}
                </th>
                <th scope="col">
                  {t('onboarding:reportResults.columns.status')}
                </th>
              </tr>
            </thead>
            <tbody>
              {reports.length === 0 ? (
                <tr>
                  <td className="nhsn-link__report-results-empty" colSpan={7}>
                    {t('onboarding:reportResults.messages.noReports')}
                  </td>
                </tr>
              ) : (
                reports.map((report) => (
                  <tr key={report.reportId}>
                    <td
                      className="nhsn-link__report-results-id"
                      title={report.reportId}>
                      <button
                        type="button"
                        className="nhsn-link__report-results-link"
                        onClick={() => handleSelectReport(report)}>
                        {report.reportId}
                      </button>
                    </td>
                    <td>
                      <span className="nhsn-link__report-results-measures">
                        {friendlyMeasuresFor(
                          report.measures,
                          report.reportId,
                          undefined,
                          reportResults.requestedMeasuresByReportId,
                        ).map((measure) => (
                          <span
                            key={measure}
                            className="nhsn-link__measure-badge"
                            style={{ background: measureColor(measure) }}>
                            {measure}
                          </span>
                        ))}
                      </span>
                    </td>
                    <td className="nhsn-link__report-results-nowrap">
                      {report.patientCount}
                    </td>
                    <td className="nhsn-link__report-results-nowrap">
                      {formatDate(report.startDate)}
                    </td>
                    <td className="nhsn-link__report-results-nowrap">
                      {formatDate(report.endDate)}
                    </td>
                    <td className="nhsn-link__report-results-nowrap">
                      {formatDateTime(report.createDate)}
                    </td>
                    <td className="nhsn-link__report-results-nowrap">
                      <span
                        className={`nhsn-link__status-pill ${STATUS_PILL_CLASS[report.status]}`}>
                        {t(`onboarding:reportResults.status.${report.status}`)}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      <div className="nhsn-link__report-results-ack">
        <CheckboxField
          id="report-results-accuracy-ack"
          label={t('onboarding:reportResults.fields.accuracyAck')}
          value={Boolean(reportAccuracyAcknowledged)}
          onChange={handleAckChange}
          disabled={!latestReportId}
        />
      </div>

      <p className="nhsn-link__form-error" role="alert">
        {validationMessage}
      </p>
    </>
  );
}

export default ReportListView;
