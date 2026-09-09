import React, {useCallback, useEffect, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {CensusListKey} from '../../../api/contracts';
import {
  Button,
  DownloadLinkButton,
  FileUploadField,
  InfoTooltip,
  InlineSpinner,
  MessageContainer,
  RepeatableList,
  Tabs,
  TextField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import {useOnboarding} from '../../OnboardingProvider';
import {CENSUS_LIST_KEYS} from '../census/validate';
import {PATIENT_ID_LIMIT, addPatientIds, isAtPatientIdLimit} from './patientIds';

type PatientTab = 'manual' | 'csv' | 'previous' | 'new-pull';

/** Same keys the census step labels its six lists with — one vocabulary, not two. */
const LIST_LABEL_KEYS: Record<CensusListKey, string> = {
  'admit-lt-24': 'onboarding:census.epic.lists.admitLt24',
  'admit-24-to-48': 'onboarding:census.epic.lists.admit24to48',
  'admit-gt-48': 'onboarding:census.epic.lists.admitGt48',
  'discharge-lt-24': 'onboarding:census.epic.lists.dischargeLt24',
  'discharge-24-to-48': 'onboarding:census.epic.lists.discharge24to48',
  'discharge-gt-48': 'onboarding:census.epic.lists.dischargeGt48'
};

/** One selectable census result — a patient list for Epic, an sFTP file for Cerner. */
interface CensusSource {
  key: string;
  label: string;
  patientIds: string[];
  simulated?: boolean;
}

interface CensusState {
  loading: boolean;
  sources?: CensusSource[];
  error?: string;
}

export interface PatientSelectionProps {
  patientIds: string[];
  onChange: (patientIds: string[]) => void;
  /** Already translated. */
  error?: string;
  disabled?: boolean;
}

/**
 * The FHIR Patient ID picker: four ways in, one list out.
 *
 * Manual Entry is the POC's editable row list and *is* the patient list; the
 * other three tabs merge into it through `addPatientIds`, so the ten-patient
 * ceiling and the duplicate rule hold whichever route the ids arrive by.
 *
 * A row can be blank while it is being typed, so the set that counts as
 * patients is the non-blank one - `validateReport` and the report request both
 * read it that way.
 */
export function PatientSelection({patientIds, onChange, error, disabled}: PatientSelectionProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError, notifyInfo, notifySuccess} = useNotifications();
  const {vendorProfile} = useOnboarding();
  const acquisition = vendorProfile?.censusAcquisition;

  const [tab, setTab] = useState<PatientTab>('manual');
  const [csvStatus, setCsvStatus] = useState<string | null>(null);

  const [previous, setPrevious] = useState<CensusState>({loading: false});
  const [newPull, setNewPull] = useState<CensusState>({loading: false});
  const [selectedSource, setSelectedSource] = useState<Record<PatientTab, string | null>>({
    manual: null,
    csv: null,
    previous: null,
    'new-pull': null
  });
  const [checked, setChecked] = useState<Record<string, boolean>>({});

  const atLimit = isAtPatientIdLimit(patientIds);

  const add = useCallback(
    (incoming: string[]): number => {
      const result = addPatientIds(patientIds, incoming);
      const reachedLimit = isAtPatientIdLimit(result.next);
      if (result.added === 0) {
        if (reachedLimit) {
          notifyError(t('onboarding:report.patients.messages.limitReached'));
        } else {
          notifyInfo(t('onboarding:report.patients.messages.noneAdded'));
        }
        return 0;
      }
      onChange(result.next);
      if (result.skipped > 0) {
        notifyInfo(
          reachedLimit
            ? t('onboarding:report.patients.messages.limitReached')
            : t('onboarding:report.patients.messages.addedSome', {
                added: result.added,
                skipped: result.skipped
              })
        );
      }
      return result.added;
    },
    [patientIds, onChange, notifyError, notifyInfo, t]
  );

  const loadSources = useCallback(async (): Promise<CensusSource[]> => {
    if (acquisition === 'Sftp') {
      const files = await api.listSftpFiles();
      return files.map(file => ({
        key: file.fileName,
        label: file.fileName,
        patientIds: file.patientIds,
        simulated: file.simulated
      }));
    }
    const results = await Promise.all(CENSUS_LIST_KEYS.map(key => api.queryPatientList(key)));
    return results.map(result => ({
      key: result.listKey,
      label: t(LIST_LABEL_KEYS[result.listKey]),
      patientIds: result.patientIds,
      simulated: result.simulated
    }));
  }, [acquisition, api, t]);

  const runPull = useCallback(
    async (which: 'previous' | 'new-pull') => {
      const set = which === 'previous' ? setPrevious : setNewPull;
      set({loading: true});
      setSelectedSource(current => ({...current, [which]: null}));
      setChecked({});
      try {
        set({loading: false, sources: await loadSources()});
      } catch (cause) {
        set({
          loading: false,
          error:
            cause instanceof Error ? cause.message : t('onboarding:report.patients.messages.pullError')
        });
      }
    },
    [loadSources, t]
  );

  useEffect(() => {
    if (tab === 'previous' && !previous.loading && !previous.sources && !previous.error) {
      void runPull('previous');
    }
  }, [tab, previous, runPull]);

  async function handleCsv(file: File) {
    setCsvStatus(t('onboarding:report.patients.csv.reading'));
    try {
      const text = await file.text();
      const lines = text
        .split(/\r\n|\n|\r/)
        .map(line => line.trim())
        .filter(Boolean);
      if (lines.length > 0 && /^"?patient\s*id"?$/i.test(lines[0])) {
        lines.shift();
      }
      const ids = lines
        .map(line => line.split(',')[0].replace(/^"|"$/g, '').trim())
        .filter(Boolean);

      if (ids.length === 0) {
        setCsvStatus(t('onboarding:report.patients.csv.noneFound', {fileName: file.name}));
        return;
      }
      const added = add(ids);
      setCsvStatus(
        t('onboarding:report.patients.csv.added', {added, total: ids.length, fileName: file.name})
      );
    } catch {
      setCsvStatus(t('onboarding:report.patients.csv.unreadable'));
    }
  }

  async function buildTemplate(): Promise<Blob> {
    return new Blob(['Patient ID\r\n123456\r\n789012\r\n'], {type: 'text/csv'});
  }

  function handleAddChecked() {
    const ids = Object.entries(checked)
      .filter(([, isChecked]) => isChecked)
      .map(([id]) => id);
    if (ids.length === 0) {
      notifyError(t('onboarding:report.patients.messages.selectAtLeastOne'));
      return;
    }
    if (add(ids) > 0) {
      setChecked({});
      notifySuccess(t('onboarding:report.patients.messages.addedFromPull', {count: ids.length}));
    }
  }

  function renderCensus(which: 'previous' | 'new-pull', state: CensusState) {
    if (!acquisition) {
      return <p className="form-hint">{t('onboarding:report.patients.census.vendorRequired')}</p>;
    }
    if (state.loading) {
      return <InlineSpinner label={t('onboarding:report.patients.census.running')} />;
    }
    if (state.error) {
      return (
        <MessageContainer type="error" showIcon>
          <span>{state.error}</span>
        </MessageContainer>
      );
    }
    if (!state.sources) {
      return null;
    }
    if (state.sources.length === 0) {
      return <p className="form-hint">{t('onboarding:report.patients.census.noSources')}</p>;
    }

    const activeKey = selectedSource[which];
    const active = activeKey ? state.sources.find(source => source.key === activeKey) : undefined;
    const available = active?.patientIds.filter(id => !patientIds.includes(id)) ?? [];

    return (
      <div className="report-census-results">
        <div className="report-source-group" role="group" aria-label={t('onboarding:report.patients.census.sourceLabel')}>
          {state.sources.map(source => (
            <button
              key={source.key}
              type="button"
              className={`report-source-btn${source.key === active?.key ? ' is-selected' : ''}`}
              aria-pressed={source.key === active?.key}
              onClick={() => {
                setSelectedSource(current => ({...current, [which]: source.key}));
                setChecked({});
              }}>
              {source.label} ({source.patientIds.length})
            </button>
          ))}
        </div>

        {!active ? (
          <p className="form-hint">{t('onboarding:report.patients.census.selectSourceHint')}</p>
        ) : (
          <>
            {available.length === 0 ? (
              <p className="form-hint">{t('onboarding:report.patients.census.emptySource')}</p>
            ) : (
              <>
                <div className="report-patient-scroll">
                  <table>
                    <thead>
                      <tr>
                        <th scope="col">
                          <span className="nhsn-link__visually-hidden">
                            {t('onboarding:report.patients.census.selectColumn')}
                          </span>
                        </th>
                        <th scope="col">{t('onboarding:report.patients.columnPatientId')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {available.map(id => (
                        <tr key={id}>
                          <td>
                            <input
                              type="checkbox"
                              checked={Boolean(checked[id])}
                              aria-label={t('onboarding:report.patients.census.selectPatient', {patientId: id})}
                              disabled={disabled}
                              onChange={event =>
                                setChecked(current => ({...current, [id]: event.target.checked}))
                              } />
                          </td>
                          <td>{id}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <Button size="sm" disabled={disabled || atLimit} onClick={handleAddChecked}>
                  {t('onboarding:report.patients.census.addSelected')}
                </Button>
              </>
            )}
          </>
        )}

        {renderSelectedPatients()}
      </div>
    );
  }

  function renderSelectedPatients() {
    const entered = patientIds.filter(id => id.trim().length > 0);
    if (entered.length === 0) {
      return null;
    }
    return (
      <div className="report-selected-patients">
        <div className="section-title">{t('onboarding:report.patients.census.selectedTitle')}</div>
        <div className="report-patient-scroll">
          <table>
            <thead>
              <tr>
                <th scope="col">{t('onboarding:report.patients.columnPatientId')}</th>
                <th scope="col">
                  <span className="nhsn-link__visually-hidden">
                    {t('common:actions.remove')}
                  </span>
                </th>
              </tr>
            </thead>
            <tbody>
              {entered.map(id => (
                <tr key={id}>
                  <td>{id}</td>
                  <td>
                    <Button
                      size="sm"
                      variant="secondary"
                      disabled={disabled}
                      aria-label={t('onboarding:report.patients.census.removeSelected', {patientId: id})}
                      onClick={() => onChange(patientIds.filter(patientId => patientId !== id))}>
                      {t('common:actions.remove')}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  return (
    <div className="form-group report-patients">
      <span className="report-patients-label" id="reportPatientsLabel">
        {t('onboarding:report.patients.label')}
        <InfoTooltip
          label={t('onboarding:report.patients.label')}
          content={t('onboarding:report.patients.hint', {limit: PATIENT_ID_LIMIT})} />
      </span>

      <Tabs<PatientTab>
        label={t('onboarding:report.patients.label')}
        activeTab={tab}
        onTabChange={setTab}
        tabs={[
          {id: 'manual', label: t('onboarding:report.patients.tabs.manual')},
          {id: 'csv', label: t('onboarding:report.patients.tabs.csv')},
          {id: 'previous', label: t('onboarding:report.patients.tabs.previous')},
          {id: 'new-pull', label: t('onboarding:report.patients.tabs.newPull')}
        ]}>
        {tab === 'manual' && (
          <div className="report-tab-body">
            <RepeatableList<string>
              items={patientIds}
              onChange={onChange}
              newItem={() => ''}
              addLabel={t('onboarding:report.patients.manual.add')}
              removeLabel={t('common:actions.remove')}
              emptyLabel={t('onboarding:report.patients.manual.empty')}
              maxItems={PATIENT_ID_LIMIT}
              disabled={disabled}
              renderItem={(item, index, onItemChange) => (
                <TextField
                  id={`reportPatientId-${index}`}
                  label={t('onboarding:report.patients.manual.inputLabel')}
                  placeholder={t('onboarding:report.patients.manual.placeholder')}
                  value={item}
                  disabled={disabled}
                  onChange={onItemChange} />
              )} />
          </div>
        )}

        {tab === 'csv' && (
          <div className="report-tab-body">
            <p className="form-hint">{t('onboarding:report.patients.csv.hint')}</p>
            <DownloadLinkButton
              buttonText={t('onboarding:report.patients.csv.template')}
              fileName="Patient_ID_Import_Template.csv"
              onDownload={buildTemplate} />
            <FileUploadField
              id="reportPatientCsv"
              label={t('onboarding:report.patients.csv.upload')}
              accept=".csv"
              disabled={disabled || atLimit}
              onSelect={file => void handleCsv(file)} />
            {csvStatus && <p className="form-hint">{csvStatus}</p>}
          </div>
        )}

        {tab === 'previous' && <div className="report-tab-body">{renderCensus('previous', previous)}</div>}

        {tab === 'new-pull' && (
          <div className="report-tab-body">
            {acquisition && !newPull.sources?.some(source => source.simulated) && (
              <Button
                size="sm"
                variant="secondary"
                disabled={disabled || newPull.loading}
                onClick={() => void runPull('new-pull')}>
                {t('onboarding:report.patients.newPull.run')}
              </Button>
            )}
            {renderCensus('new-pull', newPull)}
          </div>
        )}
      </Tabs>

      {atLimit && <p className="form-hint">{t('onboarding:report.patients.messages.limitReached')}</p>}
      {error && <p className="k-form-error">{error}</p>}
    </div>
  );
}
