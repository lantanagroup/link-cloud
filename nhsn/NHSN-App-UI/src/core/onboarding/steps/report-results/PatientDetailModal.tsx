import React from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Modal } from '../../../fields';
import { DownloadIcon } from './icons';
import { buildPieSlices } from './pieChart';
import { buildResourceBreakdown, type PatientStatusRow } from './patientRows';
import { STATUS_PILL_CLASS_BY_KEY } from './reportStatus';

export interface PatientDetailModalProps {
  open: boolean;
  onClose: () => void;
  patientRow: PatientStatusRow | null;
  reportId: string | undefined;
  currentDqmLabel: string | undefined;
  currentDqm: string | undefined;
  downloadingPatientId: string | null;
  onDownloadReport: (patientId: string, dqmId: string | undefined) => void;
  onDownloadUnavailable: () => void;
}

export function PatientDetailModal({
  open,
  onClose,
  patientRow,
  reportId,
  currentDqmLabel,
  currentDqm,
  downloadingPatientId,
  onDownloadReport,
  onDownloadUnavailable,
}: PatientDetailModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);

  const resourceBreakdown = patientRow
    ? buildResourceBreakdown(patientRow.resourceCountsByType)
    : [];
  const resourcePieSlices = buildPieSlices(resourceBreakdown, 60);

  return (
  <Modal
    open={open}
    title={t('onboarding:reportResults.detail.patientDetail.title')}
    onClose={onClose}
    size="small"
    footer={
      <Button
        variant="secondary"
        onClick={onClose}>
        {t('common:actions.close')}
      </Button>
    }>
    {patientRow && reportId && (
      <>
        <dl className="nhsn-link__report-results-detail-list">
          <div>
            <dt>
              {t('onboarding:reportResults.detail.columns.patientId')}
            </dt>
            <dd>{patientRow.patientId}</dd>
          </div>
          <div>
            <dt>{t('onboarding:reportResults.columns.reportId')}</dt>
            <dd>{reportId}</dd>
          </div>
          <div>
            <dt>
              {t('onboarding:reportResults.detail.patientDetail.measure')}
            </dt>
            <dd>{currentDqmLabel ?? '—'}</dd>
          </div>
          <div>
            <dt>
              {t(
                'onboarding:reportResults.detail.patientDetail.reportingStatus',
              )}
            </dt>
            <dd>
              <span
                className={`nhsn-link__status-pill ${STATUS_PILL_CLASS_BY_KEY[patientRow.reportStatusKey]}`}>
                {t(
                  `onboarding:reportResults.detail.statusCategories.${patientRow.reportStatusKey}`,
                )}
              </span>
            </dd>
          </div>
        </dl>
  
        <h3 className="nhsn-link__report-results-detail-section-title">
          {t(
            'onboarding:reportResults.detail.patientDetail.fhirResourceCount',
          )}
        </h3>
        {resourceBreakdown.length > 0 && (
          <div className="nhsn-link__report-results-chart-row">
            <svg
              width="120"
              height="120"
              viewBox="0 0 120 120"
              role="img"
              aria-label={t(
                'onboarding:reportResults.detail.patientDetail.fhirResourceCount',
              )}>
              {resourcePieSlices.map((slice) => (
                <path
                  key={slice.labelKey}
                  d={slice.path}
                  fill={slice.color}
                />
              ))}
            </svg>
            <ul className="nhsn-link__report-results-legend">
              {resourceBreakdown.map((slice) => (
                <li key={slice.labelKey}>
                  <span
                    className="nhsn-link__report-results-legend-dot"
                    style={{ background: slice.color }}
                  />
                  {/* Resource type is a FHIR resource type name as Report returns it -- data, not UI copy. */}
                  <span className="nhsn-link__report-results-legend-label">
                    {slice.labelKey}
                  </span>
                  <span className="nhsn-link__report-results-legend-count">
                    {slice.count} ({slice.percent}%)
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
  
        <div className="nhsn-link__report-results-patient-detail-actions">
          <Button variant="secondary" onClick={onDownloadUnavailable}>
            <DownloadIcon />
            {t(
              'onboarding:reportResults.detail.patientDetail.downloadResourceBundle',
            )}
          </Button>
          <Button
            variant="secondary"
            onClick={() => onDownloadReport(patientRow.patientId, currentDqm)}
            disabled={!currentDqm || downloadingPatientId === patientRow.patientId}
            loading={downloadingPatientId === patientRow.patientId}>
            <DownloadIcon />
            {t(
              'onboarding:reportResults.detail.patientDetail.downloadReport',
            )}
          </Button>
        </div>
      </>
    )}
  </Modal>
  );
}

export default PatientDetailModal;
