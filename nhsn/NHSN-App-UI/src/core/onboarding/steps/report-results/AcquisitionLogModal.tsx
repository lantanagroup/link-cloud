import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { AcquisitionLogEntry } from '../../../api/contracts';
import { acronymTitle, Button, HeadingPause, Modal, Select, TextField } from '../../../fields';
import { AsyncStatus } from './AsyncStatus';
import { DownloadIcon } from './icons';
import { EMPTY_ACQUISITION_LOG_FILTERS, type AcquisitionLogFilters } from './patientRows';
import { buildXlsxBlob, downloadBlob } from './reportExport';
import { buildAcquisitionLogSheet } from './xlsxSheets';

const PATIENT_ID_FILTER_ID = 'acquisition-log-filter-patient-id';

export interface AcquisitionLogModalProps {
  open: boolean;
  onClose: () => void;
  loading: boolean;
  error: string | null;
  entries: AcquisitionLogEntry[];
  reportId: string | undefined;
}

export function AcquisitionLogModal({
  open,
  onClose,
  loading,
  error,
  entries,
  reportId,
}: AcquisitionLogModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const [filters, setFilters] = useState<AcquisitionLogFilters>(
    EMPTY_ACQUISITION_LOG_FILTERS,
  );

  // Fresh filters on every open, matching the old "reset while opening" behavior -- this
  // component stays mounted (Modal itself just renders null while closed), so without this the
  // previous open's filters would still be sitting there the next time the log is reopened.
  useEffect(() => {
    if (open) {
      setFilters(EMPTY_ACQUISITION_LOG_FILTERS);
    }
  }, [open]);

  const hasAutoFocusedFilters = useRef(false);
  useEffect(() => {
    if (!open) {
      hasAutoFocusedFilters.current = false;
      return;
    }
    if (hasAutoFocusedFilters.current || loading || error || entries.length === 0) {
      return;
    }
    hasAutoFocusedFilters.current = true;
    document.getElementById(PATIENT_ID_FILTER_ID)?.focus();
  }, [open, loading, error, entries.length]);

  // Filter options are derived from whatever the report actually returned, not a fixed list --
  // a report with no Location queries simply shows no "Location" option, matching the data.
  const acquisitionResourceOptions = Array.from(
    new Set(entries.map((entry) => entry.resource)),
  )
    .filter(Boolean)
    .sort();
  const acquisitionPhaseOptions = Array.from(
    new Set(entries.map((entry) => entry.queryPhase)),
  )
    .filter(Boolean)
    .sort();
  const acquisitionTypeOptions = Array.from(
    new Set(
      entries
        .map((entry) => entry.queryType)
        .filter((value): value is string => Boolean(value)),
    ),
  ).sort();
  const acquisitionStatusOptions = Array.from(
    new Set(entries.map((entry) => entry.status)),
  )
    .filter(Boolean)
    .sort();
  const filteredEntries = entries.filter((entry) => {
    if (
      filters.patientId &&
      !entry.patientId.toLowerCase().includes(filters.patientId.toLowerCase())
    ) {
      return false;
    }
    if (filters.resource && entry.resource !== filters.resource) {
      return false;
    }
    if (filters.queryPhase && entry.queryPhase !== filters.queryPhase) {
      return false;
    }
    if (filters.queryType && entry.queryType !== filters.queryType) {
      return false;
    }
    if (filters.status && entry.status !== filters.status) {
      return false;
    }
    return true;
  });

  function handleExport() {
    if (!reportId) {
      return;
    }
    downloadBlob(
      buildXlsxBlob([buildAcquisitionLogSheet(filteredEntries)]),
      `Acquisition_Log_${reportId}.xlsx`,
    );
  }

  return (
  <Modal
    open={open}
    title={acronymTitle(
      <HeadingPause>{t('onboarding:reportResults.detail.actions.viewAcquisitionLog')}</HeadingPause>,
    )}
    onClose={onClose}
    size="large"
    footer={
      <>
        {entries.length > 0 && (
          <Button
            variant="secondary"
            onClick={() =>
              handleExport()
            }>
            <DownloadIcon />
            {t('onboarding:reportResults.detail.actions.exportToExcel')}
          </Button>
        )}
        <Button
          variant="secondary"
          onClick={onClose}>
          {t('common:actions.close')}
        </Button>
      </>
    }>
    <AsyncStatus loading={loading} error={error} />
    {!loading &&
      !error &&
      (entries.length > 0 ? (
        <>
          <div className="nhsn-link__report-results-filters">
            <TextField
              id={PATIENT_ID_FILTER_ID}
              label={t(
                'onboarding:reportResults.detail.columns.patientId',
              )}
              placeholder={t(
                'onboarding:reportResults.detail.acquisitionLog.filterPatientPlaceholder',
              )}
              value={filters.patientId}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  patientId: value,
                }))
              }
            />
            <Select
              id="acquisition-log-filter-resource"
              label={t(
                'onboarding:reportResults.detail.acquisitionLog.resource',
              )}
              placeholder={t(
                'onboarding:reportResults.detail.acquisitionLog.allResources',
              )}
              options={acquisitionResourceOptions.map((value) => ({
                value,
                label: value,
              }))}
              value={filters.resource}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  resource: value,
                }))
              }
            />
            <Select
              id="acquisition-log-filter-phase"
              label={t(
                'onboarding:reportResults.detail.acquisitionLog.queryPhase',
              )}
              placeholder={t(
                'onboarding:reportResults.detail.acquisitionLog.allPhases',
              )}
              options={acquisitionPhaseOptions.map((value) => ({
                value,
                label: value,
              }))}
              value={filters.queryPhase}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  queryPhase: value,
                }))
              }
            />
            <Select
              id="acquisition-log-filter-type"
              label={t(
                'onboarding:reportResults.detail.acquisitionLog.queryType',
              )}
              placeholder={t(
                'onboarding:reportResults.detail.acquisitionLog.allTypes',
              )}
              options={acquisitionTypeOptions.map((value) => ({
                value,
                label: value,
              }))}
              value={filters.queryType}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  queryType: value,
                }))
              }
            />
            <Select
              id="acquisition-log-filter-status"
              label={t(
                'onboarding:reportResults.detail.acquisitionLog.status',
              )}
              placeholder={t(
                'onboarding:reportResults.detail.acquisitionLog.allStatuses',
              )}
              options={acquisitionStatusOptions.map((value) => ({
                value,
                label: value,
              }))}
              value={filters.status}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  status: value,
                }))
              }
            />
          </div>
          <p
            className="nhsn-link__report-results-result-count"
            aria-live="polite">
            {t(
              'onboarding:reportResults.detail.acquisitionLog.resultCount',
              {
                shown: filteredEntries.length,
                total: entries.length,
              },
            )}
          </p>
          <div className="nhsn-link__report-results-table-scroll nhsn-link__report-results-table-scroll--compact" tabIndex={-1}>
            <table className="nhsn-link__report-results-table nhsn-link__report-results-table--fixed">
              <caption className="nhsn-link__visually-hidden">{t('onboarding:reportResults.detail.actions.viewAcquisitionLog')}</caption>
              <colgroup>
                <col style={{ width: '26%' }} />
                <col style={{ width: '16%' }} />
                <col style={{ width: '14%' }} />
                <col style={{ width: '14%' }} />
                <col style={{ width: '16%' }} />
                <col style={{ width: '14%' }} />
              </colgroup>
              <thead>
                <tr>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.columns.patientId',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.acquisitionLog.resource',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.acquisitionLog.queryPhase',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.acquisitionLog.queryType',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.acquisitionLog.parameters',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.acquisitionLog.status',
                    )}
                  </th>
                </tr>
              </thead>
              <tbody>
                {filteredEntries.map((entry, index) => (
                  <tr
                    key={`${entry.patientId}-${entry.resource}-${index}`}>
                    <td className="nhsn-link__report-results-ellipsis" title={entry.patientId}>
                      {entry.patientId}
                    </td>
                    <td className="nhsn-link__report-results-ellipsis" title={entry.resource}>
                      {entry.resource}
                    </td>
                    <td className="nhsn-link__report-results-ellipsis" title={entry.queryPhase}>
                      {entry.queryPhase}
                    </td>
                    <td className="nhsn-link__report-results-ellipsis" title={entry.queryType ?? '—'}>
                      {entry.queryType ?? '—'}
                    </td>
                    <td
                      className="nhsn-link__report-results-ellipsis"
                      title={
                        entry.parameters.length > 0
                          ? entry.parameters.join(', ')
                          : '—'
                      }>
                      {entry.parameters.length > 0
                        ? entry.parameters.join(', ')
                        : '—'}
                    </td>
                    <td className="nhsn-link__report-results-ellipsis" title={entry.status}>
                      {entry.status}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : (
        <p>{t('onboarding:reportResults.detail.acquisitionLog.empty')}</p>
      ))}
  </Modal>
  );
}

export default AcquisitionLogModal;
