import React, { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useApiClient } from '../../../api/ApiClientContext';
import type {
  AcquisitionLogEntry,
  EncounterCode,
  EncounterMapping,
  HslocCode,
  HslocMapping,
  PatientMappingEvidence,
  QueryPlan,
} from '../../../api/contracts';
import { HttpError } from '../../../api/http';
import { AcquisitionLogModal } from './AcquisitionLogModal';
import { EncounterEvidenceModal } from './EncounterEvidenceModal';
import { HslocEvidenceModal } from './HslocEvidenceModal';
import { LocationOrgEvidenceModal } from './LocationOrgEvidenceModal';
import { PatientDetailModal } from './PatientDetailModal';
import { PatientStatusTimelineModal } from './PatientStatusTimeline';
import { PreQualResultsModal } from './PreQualResults';
import { QueryPlanModal } from './QueryPlanModal';
import {
  acronymTitle,
  Button,
  HeadingPause,
  MessageContainer,
  NewTabAnnouncement,
  NHSNLoadingIndicator,
  StepActions,
  Tabs,
  TableCaption,
} from '../../../fields';
import { useNotifications } from '../../../notifications/NotificationProvider';
import { useOnboarding } from '../../OnboardingProvider';
import { useStableCallback, useStepChrome } from '../../StepChrome';
import { decodeTarget } from '../encounter/EncounterStep';
import { formatDate, formatDateTime } from './format';
import { ChartIcon, DownloadIcon, RefreshIcon } from './icons';
import { describePieSlice } from './pieChart';
import { patientsForDqm, toPatientRows, type PatientStatusRow } from './patientRows';
import { parseQueryPlan } from './queryPlan';
import { buildXlsxBlob, downloadBlob, type XlsxSheet } from './reportExport';
import { DQM_SPEC_URL_BY_ID, dqmIdForMeasureName, dqmLabel, friendlyMeasuresFor } from './reportMeasures';
import { buildReportStatusBreakdown, STATUS_PILL_CLASS, STATUS_PILL_CLASS_BY_KEY } from './reportStatus';
import {
  buildAcquisitionLogSheet,
  buildPatientReportingStatusSheet,
  buildQueryPlanSheet,
  buildReportSummarySheet,
  buildSelectedMeasuresSheet,
} from './xlsxSheets';

/**
 * The Report Details sub-view: the report's own fields, the selected measures' digital quality
 * measure links, and the View Query Plan / View Acquisition Log / Export Report Summary actions
 * from the onboarding POC, plus the per-patient mapping evidence modals. Mounted only while
 * `draft.currentView.view === 'detail'` (see ReportResultsStep), so it always starts from fresh
 * state -- there is no case where this component needs to reset its own state for a different
 * report without first unmounting.
 */
export function ReportDetailView() {
  const { t } = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const { notifySuccess, notifyError } = useNotifications();
  const { draft, mirror, saving, closeView } = useOnboarding();
  const reportResults = draft.reportResults;

  const viewingReportId = draft.currentView?.params?.reportId;

  const {
    data: detailData,
    isLoading: detailLoading,
    error: detailQueryError,
    refetch: refetchDetail,
  } = useQuery({
    queryKey: ['reportDetail', viewingReportId],
    queryFn: async () => {
      const [found, foundPatients] = await Promise.all([
        api.getReport(viewingReportId!),
        api.getReportPatients(viewingReportId!),
      ]);
      return { found, foundPatients };
    },
    enabled: Boolean(viewingReportId),
  });
  const detail = detailData?.found ?? null;
  const patients = detailData?.foundPatients ?? [];
  const detailError = detailQueryError
    ? detailQueryError instanceof Error
      ? detailQueryError.message
      : t('onboarding:reportResults.messages.detailLoadError')
    : null;
  const [activeDqm, setActiveDqm] = useState<string | undefined>();
  const [selectedPatientRow, setSelectedPatientRow] =
    useState<PatientStatusRow | null>(null);
  const [timelinePatientRow, setTimelinePatientRow] =
    useState<PatientStatusRow | null>(null);
  const [preQualPatientRow, setPreQualPatientRow] =
    useState<PatientStatusRow | null>(null);
  const [mappingEvidenceColumn, setMappingEvidenceColumn] = useState<
    'locationOrg' | 'hsloc' | 'encounter' | null
  >(null);
  const [mappingEvidencePatientId, setMappingEvidencePatientId] = useState<
    string | null
  >(null);
  const [mappingEvidence, setMappingEvidence] =
    useState<PatientMappingEvidence | null>(null);
  const [mappingEvidenceLoading, setMappingEvidenceLoading] = useState(false);
  const [mappingEvidenceError, setMappingEvidenceError] = useState<
    string | null
  >(null);

  // HSLOC reference codes, the facility's live configured mappings, and in-progress "+ Add Mapping"
  // selections for the HSLOC mapping modal's unmapped values, keyed by the unmapped source code.
  // Mappings are fetched fresh via getHslocMappings() -- the same call HslocStep itself makes --
  // rather than read off draft.hsloc.mappings: the draft only picks up HslocStep's edits when that
  // step's own Continue button patches it, so it can lag behind what saveHslocMappings actually
  // persisted (e.g. right after this modal's own "+ Add Mapping" adds one). Organization
  // Identification has no equivalent -- its 4 configuration methods make inline editing a much
  // bigger UI lift, so that modal stays read-only and links to the real step instead.
  const [hslocCodes, setHslocCodes] = useState<HslocCode[]>([]);
  const [hslocMappings, setHslocMappings] = useState<HslocMapping[]>([]);
  const [hslocDataLoading, setHslocDataLoading] = useState(false);
  const [hslocSelections, setHslocSelections] = useState<
    Record<string, string>
  >({});
  const [addingHslocCode, setAddingHslocCode] = useState<string | null>(null);

  const [encounterCodes, setEncounterCodes] = useState<EncounterCode[]>([]);
  const [encounterMappings, setEncounterMappings] = useState<EncounterMapping[]>([]);
  const [encounterDataLoading, setEncounterDataLoading] = useState(false);
  const [encounterSelections, setEncounterSelections] = useState<Record<string, string>>({});
  const [addingEncounterKey, setAddingEncounterKey] = useState<string | null>(null);

  const [queryPlanOpen, setQueryPlanOpen] = useState(false);
  const [queryPlan, setQueryPlan] = useState<QueryPlan | null>(null);
  const [queryPlanLoading, setQueryPlanLoading] = useState(false);
  const [queryPlanError, setQueryPlanError] = useState<string | null>(null);

  const [acquisitionLogOpen, setAcquisitionLogOpen] = useState(false);
  const [acquisitionLogs, setAcquisitionLogs] = useState<AcquisitionLogEntry[]>(
    [],
  );
  const [acquisitionLogLoading, setAcquisitionLogLoading] = useState(false);
  const [acquisitionLogError, setAcquisitionLogError] = useState<string | null>(
    null,
  );

  const [exporting, setExporting] = useState(false);
  const [downloadingPatientId, setDownloadingPatientId] = useState<
    string | null
  >(null);

  async function handleViewQueryPlan() {
    if (!detail) {
      return;
    }
    setQueryPlanOpen(true);
    setQueryPlanLoading(true);
    setQueryPlanError(null);
    setQueryPlan(null);
    try {
      setQueryPlan(await api.getQueryPlan(detail.reportId));
    } catch (cause) {
      // A 404 means the facility simply has no query plan configured yet -- expected, not a
      // failure. Leave queryPlan at null so the "no plan configured" empty state renders instead
      // of the raw HTTP error.
      if (!(cause instanceof HttpError && cause.status === 404)) {
        setQueryPlanError(
          cause instanceof Error
            ? cause.message
            : t('onboarding:reportResults.messages.loadError'),
        );
      }
    } finally {
      setQueryPlanLoading(false);
    }
  }

  async function handleViewAcquisitionLog() {
    if (!detail) {
      return;
    }
    setAcquisitionLogOpen(true);
    setAcquisitionLogLoading(true);
    setAcquisitionLogError(null);
    try {
      setAcquisitionLogs(await api.getAcquisitionLogs(detail.reportId));
    } catch (cause) {
      setAcquisitionLogError(
        cause instanceof Error
          ? cause.message
          : t('onboarding:reportResults.messages.loadError'),
      );
    } finally {
      setAcquisitionLogLoading(false);
    }
  }

  // Bundles every section the onboarding POC zips into separate files (mappings/report
  // details/acquisition log/query plan) into one .xlsx workbook instead -- see reportExport.ts.
  // Covers every measure and patient, not just whichever DQM tab happens to be on screen, and
  // fetches the acquisition log / query plan fresh rather than relying on those modals having
  // been opened first.
  async function handleExportSummary() {
    if (!detail) {
      return;
    }
    setExporting(true);
    try {
      const [acquisitionLog, plan] = await Promise.all([
        api
          .getAcquisitionLogs(detail.reportId)
          .catch(() => [] as AcquisitionLogEntry[]),
        api.getQueryPlan(detail.reportId).catch(() => null),
      ]);

      const sheets: XlsxSheet[] = [
        buildReportSummarySheet(detail),
        buildSelectedMeasuresSheet(
          detail,
          reportResults.requestedMeasuresByReportId,
        ),
      ];

      const patientSheet = buildPatientReportingStatusSheet(
        patients,
        detail.measureMapping,
        t,
      );
      if (patientSheet.rows.length > 0) {
        sheets.push(patientSheet);
      }

      sheets.push(buildAcquisitionLogSheet(acquisitionLog));

      const parsedPlan = plan ? parseQueryPlan(plan.planJson) : null;
      if (parsedPlan) {
        sheets.push(buildQueryPlanSheet(parsedPlan));
      }

      downloadBlob(
        buildXlsxBlob(sheets),
        `${detail.reportId}_Report_Summary.xlsx`,
      );
    } catch (cause) {
      notifyError(
        cause instanceof Error
          ? cause.message
          : t('onboarding:reportResults.messages.loadError'),
      );
    } finally {
      setExporting(false);
    }
  }

  async function handleDownloadPatientReport(
    patientId: string,
    dqmId: string | undefined,
  ) {
    if (!detail || !dqmId || downloadingPatientId === patientId) {
      return;
    }
    setDownloadingPatientId(patientId);
    try {
      const blob = await api.exportPatientReport(
        detail.reportId,
        patientId,
        dqmId,
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${patientId}_${dqmId}_report.ndjson`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (cause) {
      notifyError(
        cause instanceof Error
          ? cause.message
          : t('onboarding:reportResults.messages.loadError'),
      );
    } finally {
      setDownloadingPatientId(null);
    }
  }

  // Real evidence behind the Location Org / HSLOC / Encounter Mapping indicators, from Report's
  // per-patient detail operation. Fetched on demand rather than for every row up front.
  async function openMappingEvidence(
    column: 'locationOrg' | 'hsloc' | 'encounter',
    patientId: string,
  ) {
    if (!detail) {
      return;
    }
    setMappingEvidenceColumn(column);
    setMappingEvidencePatientId(patientId);
    setMappingEvidence(null);
    setMappingEvidenceError(null);
    setMappingEvidenceLoading(true);
    setHslocSelections({});
    setEncounterSelections({});
    try {
      const evidence = await api.getPatientMappingEvidence(
        detail.reportId,
        patientId,
      );
      setMappingEvidence(evidence);
    } catch (cause) {
      setMappingEvidenceError(
        cause instanceof Error
          ? cause.message
          : t('onboarding:reportResults.messages.loadError'),
      );
    } finally {
      setMappingEvidenceLoading(false);
    }
    if (column === 'hsloc') {
      setHslocDataLoading(true);
      try {
        // Mappings are always refetched (unlike the reference code list, cached once loaded) --
        // they can change between openings, including from this same modal's own "+ Add Mapping".
        const [codes, mappings] = await Promise.all([
          hslocCodes.length === 0
            ? api.getHslocCodes()
            : Promise.resolve(hslocCodes),
          api.getHslocMappings(),
        ]);
        setHslocCodes(codes);
        setHslocMappings(mappings);
      } catch (cause) {
        notifyError(
          cause instanceof Error
            ? cause.message
            : t('onboarding:reportResults.messages.loadError'),
        );
      } finally {
        setHslocDataLoading(false);
      }
    }
    if (column === 'encounter') {
      setEncounterDataLoading(true);
      try {
        const [codes, mappings] = await Promise.all([
          encounterCodes.length === 0 ? api.getEncounterCodes() : Promise.resolve(encounterCodes),
          api.getEncounterMappings()
        ]);
        setEncounterCodes(codes);
        setEncounterMappings(mappings);
      } catch (cause) {
        notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
      } finally {
        setEncounterDataLoading(false);
      }
    }
  }

  async function handleAddHslocMapping(unmappedCode: string) {
    const hslocCode = hslocSelections[unmappedCode];
    if (!hslocCode) {
      return;
    }
    setAddingHslocCode(unmappedCode);
    try {
      const nextMappings = [
        ...hslocMappings,
        { sourceCode: unmappedCode, hslocCode },
      ];
      await api.saveHslocMappings(nextMappings);
      setHslocMappings(nextMappings);
      mirror('hsloc', { mappings: nextMappings });
      notifySuccess(
        t('onboarding:reportResults.detail.mappingEvidence.hslocMappingAdded'),
      );
      setHslocSelections((prev) => {
        const next = { ...prev };
        delete next[unmappedCode];
        return next;
      });
    } catch (cause) {
      notifyError(
        cause instanceof Error
          ? cause.message
          : t('onboarding:reportResults.messages.loadError'),
      );
    } finally {
      setAddingHslocCode(null);
    }
  }

  async function handleAddEncounterMapping(sourceSystem: string, unmappedCode: string) {
    const key = `${sourceSystem}|${unmappedCode}`;
    const target = encounterSelections[key];
    if (!target) {
      return;
    }
    const [targetSystem, targetCode] = decodeTarget(target);
    const targetDisplay = encounterCodes.find(code => code.system === targetSystem && code.code === targetCode)?.display;
    setAddingEncounterKey(key);
    try {
      const nextMappings = [...encounterMappings, {system: sourceSystem, code: unmappedCode, display: targetDisplay, encounterType: target}];
      await api.saveEncounterMappings(nextMappings);
      setEncounterMappings(nextMappings);
      mirror('encounter', {mappings: nextMappings});
      notifySuccess(t('onboarding:reportResults.detail.mappingEvidence.encounterMappingAdded'));
      setEncounterSelections(prev => {
        const next = {...prev};
        delete next[key];
        return next;
      });
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:reportResults.messages.loadError'));
    } finally {
      setAddingEncounterKey(null);
    }
  }

  async function handleRefreshDetail() {
    if (!viewingReportId || detailLoading) {
      return;
    }
    const result = await refetchDetail();
    if (result.isSuccess) {
      notifySuccess(t('onboarding:reportResults.messages.refreshed'));
    } else {
      notifyError(t('onboarding:reportResults.messages.detailLoadError'));
    }
  }

  function handleDownloadUnavailable() {
    notifyError(t('onboarding:reportResults.detail.downloadUnavailable'));
  }

  const stableHandleViewQueryPlan = useStableCallback(handleViewQueryPlan);
  const stableHandleViewAcquisitionLog = useStableCallback(handleViewAcquisitionLog);
  const stableHandleExportSummary = useStableCallback(handleExportSummary);

  useStepChrome(
    useMemo(
      () => ({
        title: acronymTitle(<HeadingPause>{t('onboarding:reportResults.detail.title')}</HeadingPause>),
        footer: (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={closeView}>
              {t('common:actions.back')}
            </Button>
            <Button variant="secondary" onClick={stableHandleViewQueryPlan}>
              {t('onboarding:reportResults.detail.actions.viewQueryPlan')}
            </Button>
            <Button variant="secondary" onClick={stableHandleViewAcquisitionLog}>
              {t('onboarding:reportResults.detail.actions.viewAcquisitionLog')}
            </Button>
            <Button
              variant="secondary"
              onClick={stableHandleExportSummary}
              loading={exporting}
              disabled={exporting}>
              <DownloadIcon />
              {t('onboarding:reportResults.detail.actions.exportSummary')}
            </Button>
          </StepActions>
        ),
      }),
      [
        t,
        saving,
        closeView,
        stableHandleViewQueryPlan,
        stableHandleViewAcquisitionLog,
        stableHandleExportSummary,
        exporting,
      ],
    ),
  );

  const friendlyDetailMeasures = detail
    ? friendlyMeasuresFor(
        detail.measures,
        detail.reportId,
        detail.measureMapping,
        reportResults.requestedMeasuresByReportId,
      )
    : [];
  // The tabs themselves are always the report's own real dQM ids -- authoritative regardless of
  // whether a friendly name can be resolved for any of them.
  const dqmOptions = Array.from(new Set(detail?.measures ?? []));
  const currentDqm =
    activeDqm && dqmOptions.includes(activeDqm) ? activeDqm : dqmOptions[0];
  const currentDqmLabel =
    detail && currentDqm
      ? dqmLabel(currentDqm, detail.measureMapping)
      : undefined;
  // Population narrows to patients with a measure report for the active dQM -- real per-dQM
  // data. The status pie is still each patient's one overall ReportingStatus: Link has no
  // per-dQM validation outcome to split it by.
  const dqmScopedPatients = patientsForDqm(patients, currentDqm);
  const statusBreakdown = buildReportStatusBreakdown(dqmScopedPatients);
  const patientRows = toPatientRows(dqmScopedPatients, currentDqm);
  const mappingEvidencePatientRow =
    patientRows.find((row) => row.patientId === mappingEvidencePatientId) ??
    null;
  let sliceStart = 0;
  const pieSlices = statusBreakdown.map((slice) => {
    const sweep = (slice.percent / 100) * 360;
    const path = describePieSlice(60, 60, 60, sliceStart, sliceStart + sweep);
    sliceStart += sweep;
    return { ...slice, path };
  });

  return (
    <>
      <div className="nhsn-link__report-results-detail-header-actions">
        <button
          type="button"
          className={`nhsn-link__report-results-icon-button${detailLoading ? ' nhsn-link__report-results-icon-button--busy' : ''}`}
          onClick={handleRefreshDetail}
          aria-label={t('common:actions.refresh')}>
          <RefreshIcon />
        </button>
      </div>

      <p className="nhsn-link__visually-hidden" role="alert">
        {!detailLoading ? detailError : null}
      </p>

      {detailLoading && <NHSNLoadingIndicator />}

      {!detailLoading && detailError && (
        <MessageContainer type="error" showIcon>
          <span>{detailError}</span>
        </MessageContainer>
      )}

      {!detailLoading && !detailError && detail && (
        <>
          <dl className="nhsn-link__report-results-detail-list">
            <div>
              <dt>{t('onboarding:reportResults.columns.reportId')}</dt>
              <dd>{detail.reportId}</dd>
            </div>
            <div>
              <dt>{t('onboarding:reportResults.detail.reportingPeriod')}</dt>
              <dd>
                {t('onboarding:reportResults.detail.reportingPeriodValue', {
                  start: formatDate(detail.startDate),
                  end: formatDate(detail.endDate),
                })}
              </dd>
            </div>
            <div>
              <dt>{t('onboarding:reportResults.columns.createDate')}</dt>
              <dd>{formatDateTime(detail.createDate)}</dd>
            </div>
            <div>
              <dt>{t('onboarding:reportResults.columns.patientCount')}</dt>
              <dd>{detail.patientCount}</dd>
            </div>
            <div>
              <dt>{t('onboarding:reportResults.columns.status')}</dt>
              <dd>
                <span
                  className={`nhsn-link__status-pill ${STATUS_PILL_CLASS[detail.status]}`}>
                  {t(`onboarding:reportResults.status.${detail.status}`)}
                </span>
              </dd>
            </div>
          </dl>

          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.selectedMeasures')}
          </h3>
          <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
            <table className="nhsn-link__report-results-table">
              <TableCaption>{t('onboarding:reportResults.detail.selectedMeasures')}</TableCaption>
              <thead>
                <tr>
                  <th scope="col">
                    {t('onboarding:reportResults.detail.columns.nhsnMeasure')}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.columns.digitalQualityMeasure',
                    )}
                  </th>
                </tr>
              </thead>
              <tbody>
                {friendlyDetailMeasures.map((measure) => {
                  const realDqmId = dqmIdForMeasureName(measure, detail);
                  const specUrl = realDqmId
                    ? DQM_SPEC_URL_BY_ID[realDqmId]
                    : undefined;
                  return (
                    <tr key={measure}>
                      <td>{measure}</td>
                      <td>
                        {!realDqmId ? (
                          '—'
                        ) : specUrl ? (
                          <a
                            className="nhsn-link__report-results-link"
                            href={specUrl}
                            target="_blank"
                            rel="noopener noreferrer">
                            {realDqmId}
                            <NewTabAnnouncement />
                          </a>
                        ) : (
                          realDqmId
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {dqmOptions.length > 0 && (
            <Tabs
              tabs={dqmOptions.map((dqm) => ({
                id: dqm,
                label: dqmLabel(dqm, detail.measureMapping),
              }))}
              activeTab={currentDqm}
              onTabChange={setActiveDqm}
              label={t('onboarding:reportResults.detail.dqmTabsLabel')}
            />
          )}

          <h3 className="nhsn-link__report-results-detail-section-title">
            {t('onboarding:reportResults.detail.reportStatus')}
          </h3>
          {statusBreakdown.length > 0 && (
            <div className="nhsn-link__report-results-chart-row">
              <svg
                width="120"
                height="120"
                viewBox="0 0 120 120"
                role="img"
                aria-label={t(
                  'onboarding:reportResults.detail.reportStatus',
                )}>
                {pieSlices.map((slice) => (
                  <path
                    key={slice.labelKey}
                    d={slice.path}
                    fill={slice.color}
                  />
                ))}
              </svg>
              <ul className="nhsn-link__report-results-legend">
                {statusBreakdown.map((slice) => (
                  <li key={slice.labelKey}>
                    <span
                      className="nhsn-link__report-results-legend-dot"
                      style={{ background: slice.color }}
                    />
                    <span className="nhsn-link__report-results-legend-label">
                      {t(
                        `onboarding:reportResults.detail.statusCategories.${slice.labelKey}`,
                      )}
                    </span>
                    <span className="nhsn-link__report-results-legend-count">
                      {slice.count} ({slice.percent}%)
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {patientRows.length > 0 && (
            <>
              <h3 className="nhsn-link__report-results-detail-section-title">
                {t('onboarding:reportResults.detail.patientReportingStatus', {
                  count: dqmScopedPatients.length,
                })}
              </h3>
              <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
                <table className="nhsn-link__report-results-table nhsn-link__report-results-table--light-border nhsn-link__report-results-table--fixed">
                  <TableCaption>
                    {t('onboarding:reportResults.detail.patientReportingStatus', {
                      count: dqmScopedPatients.length,
                    })}
                  </TableCaption>
                  <colgroup>
                    <col style={{ width: '9%' }} />
                    <col style={{ width: '12%' }} />
                    <col style={{ width: '18%' }} />
                    <col style={{ width: '11%' }} />
                    <col style={{ width: '11%' }} />
                    <col style={{ width: '13%' }} />
                    <col style={{ width: '13%' }} />
                    <col style={{ width: '13%' }} />
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
                          'onboarding:reportResults.detail.columns.fhirResourceCount',
                        )}
                      </th>
                      <th scope="col">
                        {t(
                          'onboarding:reportResults.detail.columns.reportStatus',
                        )}
                      </th>
                      <th scope="col">
                        {t(
                          'onboarding:reportResults.detail.columns.preQualResults',
                        )}
                      </th>
                      <th scope="col">
                        {t(
                          'onboarding:reportResults.detail.columns.locationOrg',
                        )}
                      </th>
                      <th scope="col">
                        {t(
                          'onboarding:reportResults.detail.columns.hslocMapping',
                        )}
                      </th>
                      <th scope="col">
                        {t(
                          'onboarding:reportResults.detail.columns.encounterMapping',
                        )}
                      </th>
                      <th aria-hidden="true" />
                    </tr>
                  </thead>
                  <tbody>
                    {patientRows.map((row) => (
                      <tr key={row.patientId}>
                        <td>
                          <button
                            type="button"
                            className="nhsn-link__report-results-link"
                            onClick={() => setSelectedPatientRow(row)}>
                            {row.patientId}
                          </button>
                        </td>
                        <td>{row.fhirResourceCount}</td>
                        <td>
                          <button
                            type="button"
                            className="nhsn-link__status-pill-button"
                            onClick={() => setTimelinePatientRow(row)}
                            aria-label={t(
                              'onboarding:reportResults.detail.patientTimeline.title',
                            )}
                            title={t(
                              'onboarding:reportResults.detail.patientTimeline.title',
                            )}>
                            <span
                              className={`nhsn-link__status-pill ${STATUS_PILL_CLASS_BY_KEY[row.reportStatusKey]}`}>
                              {t(
                                `onboarding:reportResults.detail.statusCategories.${row.reportStatusKey}`,
                              )}
                            </span>
                          </button>
                        </td>
                        <td>
                          {row.hasPreQualResults ? (
                            <button
                              type="button"
                              className="nhsn-link__report-results-icon-button"
                              onClick={() => setPreQualPatientRow(row)}
                              aria-label={t(
                                'onboarding:reportResults.detail.columns.preQualResults',
                              )}
                              title={t(
                                'onboarding:reportResults.detail.columns.preQualResults',
                              )}>
                              <ChartIcon />
                            </button>
                          ) : (
                            t('onboarding:reportResults.detail.notApplicable')
                          )}
                        </td>
                        <td>
                          <button
                            type="button"
                            className="nhsn-link__status-pill-button"
                            onClick={() =>
                              openMappingEvidence(
                                'locationOrg',
                                row.patientId,
                              )
                            }>
                            <span
                              className={`nhsn-link__mapping-pill ${row.locationOrgFound ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                              {row.locationOrgFound
                                ? t(
                                    'onboarding:reportResults.detail.mapping.found',
                                  )
                                : t(
                                    'onboarding:reportResults.detail.mapping.notFound',
                                  )}
                            </span>
                          </button>
                        </td>
                        <td>
                          <button
                            type="button"
                            className="nhsn-link__status-pill-button"
                            onClick={() =>
                              openMappingEvidence('hsloc', row.patientId)
                            }>
                            <span
                              className={`nhsn-link__mapping-pill ${row.hslocFound ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                              {row.hslocFound
                                ? t(
                                    'onboarding:reportResults.detail.mapping.found',
                                  )
                                : t(
                                    'onboarding:reportResults.detail.mapping.notFound',
                                  )}
                            </span>
                          </button>
                        </td>
                        <td>
                          <button
                            type="button"
                            className="nhsn-link__status-pill-button"
                            onClick={() =>
                              openMappingEvidence('encounter', row.patientId)
                            }>
                            <span
                              className={`nhsn-link__mapping-pill ${row.encounterFound ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                              {row.encounterFound
                                ? t(
                                    'onboarding:reportResults.detail.mapping.found',
                                  )
                                : t(
                                    'onboarding:reportResults.detail.mapping.notFound',
                                  )}
                            </span>
                          </button>
                        </td>
                        <td>
                          <button
                            type="button"
                            className={`nhsn-link__report-results-icon-button${downloadingPatientId === row.patientId ? ' nhsn-link__report-results-icon-button--busy' : ''}`}
                            onClick={() =>
                              handleDownloadPatientReport(
                                row.patientId,
                                currentDqm,
                              )
                            }
                            aria-label={t(
                              'onboarding:reportResults.detail.downloadPatientReport',
                            )}
                            title={t(
                              'onboarding:reportResults.detail.downloadPatientReport',
                            )}>
                            <DownloadIcon />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}

      <QueryPlanModal
        open={queryPlanOpen}
        onClose={() => setQueryPlanOpen(false)}
        loading={queryPlanLoading}
        error={queryPlanError}
        queryPlan={queryPlan}
      />

      <AcquisitionLogModal
        open={acquisitionLogOpen}
        onClose={() => setAcquisitionLogOpen(false)}
        loading={acquisitionLogLoading}
        error={acquisitionLogError}
        entries={acquisitionLogs}
        reportId={detail?.reportId}
      />

      <PatientDetailModal
        open={Boolean(selectedPatientRow)}
        onClose={() => setSelectedPatientRow(null)}
        patientRow={selectedPatientRow}
        reportId={detail?.reportId}
        currentDqmLabel={currentDqmLabel}
        currentDqm={currentDqm}
        downloadingPatientId={downloadingPatientId}
        onDownloadReport={handleDownloadPatientReport}
        onDownloadUnavailable={handleDownloadUnavailable}
      />

      {timelinePatientRow && (
        <PatientStatusTimelineModal
          open
          onClose={() => setTimelinePatientRow(null)}
          patientId={timelinePatientRow.patientId}
          measureName={currentDqmLabel}
          reportingStatus={timelinePatientRow.reportingStatus}
        />
      )}

      {preQualPatientRow && detail && (
        <PreQualResultsModal
          open
          onClose={() => setPreQualPatientRow(null)}
          patientId={preQualPatientRow.patientId}
          measureName={currentDqmLabel}
          reportingStatus={preQualPatientRow.reportingStatus}
          reportId={detail.reportId}
        />
      )}

      {/* Location Org Mapping -- read-only: the facility's configured mappings plus the real
          per-patient evidence Report recorded. Unlike HSLOC below, there is no save call for
          Location Org config that isn't gated to the Organization Identification step itself
          (see ApiClient.saveDraft), so "fixing" a miss here just links there instead of faking
          an inline edit that wouldn't persist. */}
      <LocationOrgEvidenceModal
        open={mappingEvidenceColumn === 'locationOrg'}
        onClose={() => setMappingEvidenceColumn(null)}
        patientId={mappingEvidencePatientId}
        patientRow={mappingEvidencePatientRow}
        loading={mappingEvidenceLoading}
        error={mappingEvidenceError}
        evidence={mappingEvidence}
      />

      {/* HSLOC Mapping -- the one mapping type this screen can actually fix: saveHslocMappings is
          a standalone call, not gated to the Location Identification step the way saveDraft is
          (see handleAddHslocMapping above), so an unmapped value acquired for this patient gets a
          real "+ Add Mapping" control. */}
      <HslocEvidenceModal
        open={mappingEvidenceColumn === 'hsloc'}
        onClose={() => setMappingEvidenceColumn(null)}
        patientId={mappingEvidencePatientId}
        evidence={mappingEvidence}
        loading={mappingEvidenceLoading}
        error={mappingEvidenceError}
        codes={hslocCodes}
        mappings={hslocMappings}
        dataLoading={hslocDataLoading}
        selections={hslocSelections}
        onSelectionChange={(code, value) =>
          setHslocSelections((prev) => ({ ...prev, [code]: value }))
        }
        addingCode={addingHslocCode}
        onAddMapping={handleAddHslocMapping}
      />

      <EncounterEvidenceModal
        open={mappingEvidenceColumn === 'encounter'}
        onClose={() => setMappingEvidenceColumn(null)}
        patientId={mappingEvidencePatientId}
        patientRow={mappingEvidencePatientRow}
        evidence={mappingEvidence}
        loading={mappingEvidenceLoading}
        error={mappingEvidenceError}
        codes={encounterCodes}
        mappings={encounterMappings}
        dataLoading={encounterDataLoading}
        selections={encounterSelections}
        onSelectionChange={(key, value) =>
          setEncounterSelections((prev) => ({ ...prev, [key]: value }))
        }
        addingKey={addingEncounterKey}
        onAddMapping={handleAddEncounterMapping}
      />
    </>
  );
}

export default ReportDetailView;
