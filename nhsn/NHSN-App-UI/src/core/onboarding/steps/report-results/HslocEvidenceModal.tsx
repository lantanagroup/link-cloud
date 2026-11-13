import React, { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  HslocCode,
  HslocMapping,
  PatientMappingEvidence,
} from '../../../api/contracts';
import { AcronymText, acronymTitle, Button, HeadingPause, MessageContainer, Modal, NHSNLoadingIndicator, Select, TextField } from '../../../fields';
import { useOnboarding } from '../../OnboardingProvider';
import { AsyncStatus } from './AsyncStatus';
import type { HslocDiscoveryRow } from './hslocDiscovery';
import { isHslocCodeMap } from './patientRows';

export interface HslocEvidenceModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string | null;
  evidence: PatientMappingEvidence | null;
  loading: boolean;
  error: string | null;
  codes: HslocCode[];
  mappings: HslocMapping[];
  dataLoading: boolean;
  selections: Record<string, string>;
  onSelectionChange: (code: string, value: string) => void;
  addingCode: string | null;
  onAddMapping: (code: string, sourceDisplay?: string) => void;
  displayInputs: Record<string, string>;
  onDisplayInputChange: (code: string, value: string) => void;
  discoveryRows: HslocDiscoveryRow[] | null;
  notReportable: boolean;
}

interface AcquiredValueRow {
  key: string;
  code: string;
  rawLabel: string;
  found: boolean;
  matchedHslocCode: string | null;
  matchedSourceDisplay: string | null;
}

// Discovery (parsed from this patient's export) covers every non-HSLOC coding found; without it
// (not reportable, or the export fetch failed) this falls back to Report's own recorded evidence,
// which only ever lists codes already flagged unmapped.
function acquiredValueRows(
  discoveryRows: HslocDiscoveryRow[] | null,
  evidence: PatientMappingEvidence | null,
  mappings: HslocMapping[],
): AcquiredValueRow[] | null {
  if (discoveryRows) {
    return discoveryRows.map((row) => ({
      key: row.key,
      code: row.code,
      rawLabel: row.display ? `${row.code} (${row.display})` : row.code,
      found: row.found,
      matchedHslocCode: row.matchedHslocCode ?? null,
      matchedSourceDisplay: row.matchedSourceDisplay ?? null,
    }));
  }
  if (evidence === null) {
    return null;
  }
  const mappingBySourceCode = new Map(mappings.map((mapping) => [mapping.sourceCode, mapping]));
  const unmappedCodes = Array.from(
    new Set(
      evidence.codeMaps.filter(isHslocCodeMap).flatMap((codeMap) => codeMap.unmappedCodes),
    ),
  ).filter((code) => !mappingBySourceCode.has(code));
  return unmappedCodes.map((code) => ({
    key: code,
    code,
    rawLabel: code,
    found: false,
    matchedHslocCode: null,
    matchedSourceDisplay: null,
  }));
}

export function HslocEvidenceModal({
  open,
  onClose,
  patientId,
  evidence,
  loading,
  error,
  codes,
  mappings,
  dataLoading,
  selections,
  onSelectionChange,
  addingCode,
  onAddMapping,
  displayInputs,
  onDisplayInputChange,
  discoveryRows,
  notReportable,
}: HslocEvidenceModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const { vendorProfile, user } = useOnboarding();
  const localCodeEnabled = Boolean(user.capabilities?.hslocLocationDisplayUpdate);
  const configuredMappingsHeadingId = useId();
  const acquiredValueHeadingId = useId();
  const sourceLabel =
    vendorProfile?.hslocSourceLabel ?? t('onboarding:hsloc.mapping.fields.locationValueFallback');

  const rows = acquiredValueRows(discoveryRows, evidence, mappings);

  return (
  <Modal
    open={open}
    title={acronymTitle(
      <HeadingPause><AcronymText>{t('onboarding:reportResults.detail.mappingEvidence.hslocTitle')}</AcronymText></HeadingPause>,
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

    <h3 id={configuredMappingsHeadingId} className="nhsn-link__report-results-detail-section-title">
      <AcronymText>
        {t(
          'onboarding:reportResults.detail.mappingEvidence.configuredHslocMappings',
        )}
      </AcronymText>
    </h3>
    <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
      <table className="nhsn-link__report-results-table" aria-labelledby={configuredMappingsHeadingId}>
        <thead>
          <tr>
            {localCodeEnabled && (
              <th scope="col">
                {t(
                  'onboarding:reportResults.detail.mappingEvidence.yourCode',
                )}
              </th>
            )}
            <th scope="col">
              {sourceLabel}
            </th>
            <th scope="col">
              <AcronymText>
                {t(
                  'onboarding:reportResults.detail.mappingEvidence.hslocCode',
                )}
              </AcronymText>
            </th>
          </tr>
        </thead>
        <tbody>
          {dataLoading ? (
            <tr>
              <td colSpan={localCodeEnabled ? 3 : 2}>
                <NHSNLoadingIndicator />
              </td>
            </tr>
          ) : mappings.length === 0 ? (
            <tr>
              <td colSpan={localCodeEnabled ? 3 : 2}>
                {t(
                  'onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings',
                )}
              </td>
            </tr>
          ) : (
            mappings.map((mapping, index) => (
              <tr key={`${mapping.sourceCode}-${index}`}>
                {localCodeEnabled && <td>{mapping.sourceDisplay || '—'}</td>}
                <td>{mapping.sourceCode}</td>
                <td>{mapping.hslocCode}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>

    <AsyncStatus loading={loading} error={error} />
    {notReportable && (
      <MessageContainer type="info" showIcon>
        <p>
          {t(
            'onboarding:reportResults.detail.mappingEvidence.notReportableHint',
          )}
        </p>
      </MessageContainer>
    )}

    {!loading &&
      !error &&
      rows && (
        <>
          <h3 id={acquiredValueHeadingId} className="nhsn-link__report-results-detail-section-title">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.acquiredValueHeading',
            )}
          </h3>
          {dataLoading ? (
            <NHSNLoadingIndicator />
          ) : rows.length === 0 ? (
            <p>
              {t(
                discoveryRows
                  ? 'onboarding:reportResults.detail.mappingEvidence.allResolved'
                  : 'onboarding:reportResults.detail.mappingEvidence.noEvidence',
              )}
            </p>
          ) : (
            <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
              <table className="nhsn-link__report-results-table" aria-labelledby={acquiredValueHeadingId}>
                <thead>
                  <tr>
                    {localCodeEnabled && (
                      <th scope="col">
                        {t('onboarding:reportResults.detail.mappingEvidence.yourCode')}
                      </th>
                    )}
                    <th scope="col">{sourceLabel}</th>
                    <th scope="col">
                      <AcronymText>
                        {t(
                          'onboarding:reportResults.detail.mappingEvidence.hslocCode',
                        )}
                      </AcronymText>
                    </th>
                    <th scope="col">
                      {t('onboarding:reportResults.detail.mappingEvidence.status')}
                    </th>
                    <th aria-hidden="true" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr
                      key={row.key}
                      className={row.found ? undefined : 'nhsn-link__report-results-row--unmapped'}>
                      {localCodeEnabled && (
                        <td>
                          {row.found ? (
                            row.matchedSourceDisplay || '—'
                          ) : (
                            <TextField
                              id={`hsloc-your-code-${row.code}`}
                              label={t(
                                'onboarding:reportResults.detail.mappingEvidence.yourCode',
                              )}
                              required
                              value={displayInputs[row.code] ?? ''}
                              onChange={(value) => onDisplayInputChange(row.code, value)}
                            />
                          )}
                        </td>
                      )}
                      <td>{row.rawLabel}</td>
                      <td>
                        {row.found ? (
                          row.matchedHslocCode ?? '—'
                        ) : (
                          <Select
                            id={`hsloc-add-${row.code}`}
                            label={t(
                              'onboarding:reportResults.detail.mappingEvidence.hslocCode',
                            )}
                            placeholder={t(
                              'onboarding:reportResults.detail.mappingEvidence.selectHslocCode',
                            )}
                            options={codes.map((hslocCode) => ({
                              value: hslocCode.code,
                              label: `${hslocCode.code} - ${hslocCode.display}`,
                            }))}
                            value={selections[row.code] ?? ''}
                            onChange={(value) => onSelectionChange(row.code, value)}
                          />
                        )}
                      </td>
                      <td>
                        <span
                          className={`nhsn-link__mapping-pill ${row.found ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                          {row.found
                            ? t('onboarding:reportResults.detail.mapping.found')
                            : t('onboarding:reportResults.detail.mapping.notFound')}
                        </span>
                      </td>
                      <td>
                        {!row.found && (
                          <Button
                            variant="secondary"
                            onClick={() => onAddMapping(row.code, displayInputs[row.code])}
                            disabled={
                              !selections[row.code] ||
                              (localCodeEnabled &&
                                !(displayInputs[row.code] ?? '').trim()) ||
                              addingCode === row.code
                            }
                            loading={addingCode === row.code}>
                            {t(
                              'onboarding:reportResults.detail.mappingEvidence.addMapping',
                            )}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {rows.some((row) => !row.found) && (
            <p className="nhsn-link__report-results-hint-box">
              {t(
                'onboarding:reportResults.detail.mappingEvidence.hslocAddedHint',
              )}
            </p>
          )}
        </>
      )}
  </Modal>
  );
}

export default HslocEvidenceModal;
