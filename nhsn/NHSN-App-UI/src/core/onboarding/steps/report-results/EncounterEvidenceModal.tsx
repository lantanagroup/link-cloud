import React from 'react';
import { useTranslation } from 'react-i18next';
import type {
  EncounterCode,
  EncounterMapping,
  PatientMappingEvidence,
} from '../../../api/contracts';
import {
  Button,
  MessageContainer,
  Modal,
  NHSNLoadingIndicator,
  Select,
} from '../../../fields';
import { useOnboarding } from '../../OnboardingProvider';
import { buildGroups, encodeTarget } from '../encounter/EncounterStep';
import { AsyncStatus } from './AsyncStatus';
import { isHslocCodeMap, type PatientStatusRow } from './patientRows';

export interface EncounterEvidenceModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string | null;
  patientRow: PatientStatusRow | null;
  evidence: PatientMappingEvidence | null;
  loading: boolean;
  error: string | null;
  codes: EncounterCode[];
  mappings: EncounterMapping[];
  dataLoading: boolean;
  selections: Record<string, string>;
  onSelectionChange: (key: string, value: string) => void;
  addingKey: string | null;
  onAddMapping: (sourceSystem: string, code: string) => void;
}

export function EncounterEvidenceModal({
  open,
  onClose,
  patientId,
  patientRow,
  evidence,
  loading,
  error,
  codes,
  mappings,
  dataLoading,
  selections,
  onSelectionChange,
  addingKey,
  onAddMapping,
}: EncounterEvidenceModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const { draft, goTo } = useOnboarding();

  const encounterCodeMaps = evidence
    ? evidence.codeMaps.filter((codeMap) => !isHslocCodeMap(codeMap))
    : [];
  const encounterGroups = buildGroups(draft.encounter.codeSystems ?? [], mappings);
  const encounterUnmappedEntries = encounterCodeMaps.flatMap((codeMap) =>
    codeMap.unmappedCodes.map((code) => ({
      sourceSystem: codeMap.sourceSystem,
      code,
      key: `${codeMap.sourceSystem}|${code}`,
    })),
  );

  return (
  <Modal
    open={open}
    title={t(
      'onboarding:reportResults.detail.mappingEvidence.encounterTitle',
    )}
    onClose={onClose}
    size="large"
    footer={
      <Button
        variant="secondary"
        onClick={onClose}>
        {t('common:actions.close')}
      </Button>
    }>
    <dl className="nhsn-link__report-results-detail-list">
      <div>
        <dt>{t('onboarding:reportResults.detail.columns.patientId')}</dt>
        <dd>{patientId}</dd>
      </div>
    </dl>
  
    <AsyncStatus loading={loading} error={error} />

    {!loading &&
      !error &&
      encounterCodeMaps.length > 0 && (
        <>
          <h3 className="nhsn-link__report-results-detail-section-title">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.acquiredEncounterValueHeading',
            )}
          </h3>
          <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
            <table className="nhsn-link__report-results-table">
              <caption className="nhsn-link__visually-hidden">
                {t(
                  'onboarding:reportResults.detail.mappingEvidence.acquiredEncounterValueHeading',
                )}
              </caption>
              <thead>
                <tr>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.mappingEvidence.sourceSystem',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.mappingEvidence.unmappedCodes',
                    )}
                  </th>
                </tr>
              </thead>
              <tbody>
                {encounterCodeMaps.map((codeMap, index) => (
                  <tr key={`${codeMap.sourceSystem}-${index}`}>
                    <td>{codeMap.sourceSystem}</td>
                    <td>
                      {codeMap.unmappedCodes.length > 0
                        ? codeMap.unmappedCodes.join(', ')
                        : '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
  
    <h3 className="nhsn-link__report-results-detail-section-title">
      {t(
        'onboarding:reportResults.detail.mappingEvidence.configuredEncounterCodeSystems',
      )}
    </h3>
    {dataLoading ? (
      <NHSNLoadingIndicator />
    ) : encounterGroups.length === 0 ? (
      <p className="nhsn-link__hint-text">
        {t(
          'onboarding:reportResults.detail.mappingEvidence.noCodeSystemsConfigured',
        )}
      </p>
    ) : (
      encounterGroups.map((group) => (
        <div key={group.groupKey} className="nhsn-link__field-group">
          <h4 className="nhsn-link__report-results-detail-section-title">
            {group.codeSystem ||
              t(
                'onboarding:reportResults.detail.mappingEvidence.codeSystem',
              )}
          </h4>
          <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
            <table className="nhsn-link__report-results-table">
              <caption className="nhsn-link__visually-hidden">
                {group.codeSystem ||
                  t(
                    'onboarding:reportResults.detail.mappingEvidence.codeSystem',
                  )}
              </caption>
              <thead>
                <tr>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.mappingEvidence.localValue',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.mappingEvidence.standardSystem',
                    )}
                  </th>
                  <th scope="col">
                    {t(
                      'onboarding:reportResults.detail.mappingEvidence.standardCode',
                    )}
                  </th>
                </tr>
              </thead>
              <tbody>
                {group.mappings.length === 0 ? (
                  <tr>
                    <td colSpan={3}>
                      {t(
                        'onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings',
                      )}
                    </td>
                  </tr>
                ) : (
                  group.mappings.map((row) => (
                    <tr key={row.rowKey}>
                      <td>{row.localValue}</td>
                      <td>{row.targetSystem || '—'}</td>
                      <td>{row.targetCode || '—'}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      ))
    )}
  
    {!dataLoading && encounterUnmappedEntries.length > 0 && (
      <>
        <h3 className="nhsn-link__report-results-detail-section-title">
          {t(
            'onboarding:reportResults.detail.mappingEvidence.acquiredValueHeading',
          )}
        </h3>
        <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
          <table className="nhsn-link__report-results-table">
            <caption className="nhsn-link__visually-hidden">
              {t(
                'onboarding:reportResults.detail.mappingEvidence.acquiredValueHeading',
              )}
            </caption>
            <thead>
              <tr>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.sourceSystem',
                  )}
                </th>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.yourCode',
                  )}
                </th>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.targetSystem',
                  )}
                </th>
                <th aria-hidden="true" />
              </tr>
            </thead>
            <tbody>
              {encounterUnmappedEntries.map((entry) => (
                <tr key={entry.key}>
                  <td>{entry.sourceSystem}</td>
                  <td>{entry.code}</td>
                  <td>
                    <Select
                      id={`encounter-add-${entry.key}`}
                      label={t(
                        'onboarding:reportResults.detail.mappingEvidence.targetSystem',
                      )}
                      placeholder={t(
                        'onboarding:reportResults.detail.mappingEvidence.selectTargetCode',
                      )}
                      options={codes.map((code) => ({
                        value: encodeTarget(code.system, code.code),
                        label: `${code.code} - ${code.display}`,
                      }))}
                      value={selections[entry.key] ?? ''}
                      onChange={(value) => onSelectionChange(entry.key, value)}
                    />
                  </td>
                  <td>
                    <Button
                      variant="secondary"
                      onClick={() => onAddMapping(entry.sourceSystem, entry.code)}
                      disabled={
                        !selections[entry.key] ||
                        addingKey === entry.key
                      }
                      loading={addingKey === entry.key}>
                      {t(
                        'onboarding:reportResults.detail.mappingEvidence.addMapping',
                      )}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="nhsn-link__hint-text">
          {t(
            'onboarding:reportResults.detail.mappingEvidence.encounterAddedHint',
          )}
        </p>
      </>
    )}
  
    {patientRow &&
      !patientRow.encounterFound && (
        <MessageContainer type="info" showIcon>
          <p role="status">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.notFoundEncounterHint',
            )}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              onClose();
              goTo('encounter');
            }}>
            {t(
              'onboarding:reportResults.detail.mappingEvidence.goToEncounterMapping',
            )}
          </Button>
        </MessageContainer>
      )}
  </Modal>
  );
}

export default EncounterEvidenceModal;
