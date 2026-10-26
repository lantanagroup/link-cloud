import React from 'react';
import { useTranslation } from 'react-i18next';
import type { QueryPlan } from '../../../api/contracts';
import { AcronymText, acronymTitle, Button, HeadingPause, Modal } from '../../../fields';
import { AsyncStatus } from './AsyncStatus';
import { DownloadIcon } from './icons';
import { parseQueryPlan } from './queryPlan';
import { buildXlsxBlob, downloadBlob } from './reportExport';
import { buildQueryPlanSheet } from './xlsxSheets';

export interface QueryPlanModalProps {
  open: boolean;
  onClose: () => void;
  loading: boolean;
  error: string | null;
  queryPlan: QueryPlan | null;
}

export function QueryPlanModal({
  open,
  onClose,
  loading,
  error,
  queryPlan,
}: QueryPlanModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const parsedQueryPlan = queryPlan ? parseQueryPlan(queryPlan.planJson) : null;

  function handleExportQueryPlan() {
    if (!parsedQueryPlan || !queryPlan) {
      return;
    }
    downloadBlob(
      buildXlsxBlob([buildQueryPlanSheet(parsedQueryPlan)]),
      `Query_Plan_${queryPlan.reportId}.xlsx`,
    );
  }

  return (
    <Modal
      open={open}
      title={acronymTitle(<HeadingPause>{t('onboarding:reportResults.detail.actions.viewQueryPlan')}</HeadingPause>)}
      onClose={onClose}
      size="large"
      footer={
        <>
          {parsedQueryPlan && (
            <Button variant="secondary" onClick={handleExportQueryPlan}>
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
      {!loading && !error && parsedQueryPlan && (
        <>
          <ul className="nhsn-link__summary-list">
            <li>
              <span>
                <AcronymText>{t('onboarding:reportResults.detail.queryPlan.ehrType')}</AcronymText>
              </span>
              <span>{parsedQueryPlan.ehrDescription ?? '—'}</span>
            </li>
          </ul>
    
          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.queryPlan.planDetails')}
          </h3>
          <div className="nhsn-link__report-results-table-scroll nhsn-link__report-results-table-scroll--compact" tabIndex={-1}>
            <table className="nhsn-link__report-results-table">
              <caption className="nhsn-link__visually-hidden">{t('onboarding:reportResults.detail.queryPlan.planDetails')}</caption>
              <tbody>
                <tr>
                  <td>
                    {t(
                      'onboarding:reportResults.detail.queryPlan.planName',
                    )}
                  </td>
                  <td>{parsedQueryPlan.planName ?? '—'}</td>
                </tr>
                <tr>
                  <td>
                    {t(
                      'onboarding:reportResults.detail.queryPlan.lookBack',
                    )}
                  </td>
                  <td>{parsedQueryPlan.lookBack ?? '—'}</td>
                </tr>
              </tbody>
            </table>
          </div>
    
          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.queryPlan.queries')}
          </h3>
          <div className="nhsn-link__report-results-table-scroll nhsn-link__report-results-table-scroll--compact" tabIndex={-1}>
            <table className="nhsn-link__report-results-table">
              <caption className="nhsn-link__visually-hidden">{t('onboarding:reportResults.detail.queryPlan.queries')}</caption>
              <thead>
                <tr>
                  <th scope="col">
                    {t('onboarding:reportResults.detail.queryPlan.section')}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.queryPlan.resourceType',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.queryPlan.queryType',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.queryPlan.operationType',
                    )}
                  </th>
                  <th scope="col">
                    {t('onboarding:reportResults.detail.queryPlan.paged')}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.queryPlan.parameters',
                    )}
                  </th>
                </tr>
              </thead>
              <tbody>
                {[
                  ...parsedQueryPlan.initialQueries.map((query) => ({
                    section: 'Initial',
                    query,
                  })),
                  ...parsedQueryPlan.supplementalQueries.map((query) => ({
                    section: 'Supplemental',
                    query,
                  })),
                ].map(({ section, query }, index) => (
                  <tr key={`${section}-${query.resourceType}-${index}`}>
                    <td>{section}</td>
                    <td>{query.resourceType}</td>
                    <td>{query.queryConfigType ?? '—'}</td>
                    <td>{query.operationType ?? '—'}</td>
                    <td>
                      {query.paged === undefined
                        ? '—'
                        : query.paged
                          ? t('common:actions.yes')
                          : t('common:actions.no')}
                    </td>
                    <td>
                      {query.parameters.length > 0
                        ? query.parameters.join(', ')
                        : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
      {!loading &&
        !error &&
        !parsedQueryPlan &&
        (queryPlan ? (
          <pre className="nhsn-link__report-results-json">
            {queryPlan.planJson}
          </pre>
        ) : (
          <p>{t('onboarding:reportResults.detail.queryPlan.empty')}</p>
        ))}
    </Modal>
  );
}

export default QueryPlanModal;
