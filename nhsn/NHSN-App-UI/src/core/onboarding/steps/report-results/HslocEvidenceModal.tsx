import React, { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type {
  HslocCode,
  HslocMapping,
  PatientMappingEvidence,
} from '../../../api/contracts';
import { AcronymText, acronymTitle, Button, HeadingPause, Modal, NHSNLoadingIndicator, Select } from '../../../fields';
import { useOnboarding } from '../../OnboardingProvider';
import { AsyncStatus } from './AsyncStatus';
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
  onAddMapping: (code: string) => void;
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
}: HslocEvidenceModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const { vendorProfile } = useOnboarding();
  const configuredMappingsHeadingId = useId();
  const acquiredValueHeadingId = useId();

  const mappedCodes = new Set(mappings.map((mapping) => mapping.sourceCode));
  const hslocUnmappedCodes = evidence
    ? Array.from(
        new Set(
          evidence.codeMaps
            .filter(isHslocCodeMap)
            .flatMap((codeMap) => codeMap.unmappedCodes),
        ),
      ).filter((code) => !mappedCodes.has(code))
    : [];

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
            <th scope="col">
              {t(
                'onboarding:reportResults.detail.mappingEvidence.yourCode',
              )}
            </th>
            <th scope="col">
              {vendorProfile?.hslocSourceLabel ??
                t(
                  'onboarding:hsloc.mapping.fields.locationValueFallback',
                )}
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
              <td colSpan={3}>
                <NHSNLoadingIndicator />
              </td>
            </tr>
          ) : mappings.length === 0 ? (
            <tr>
              <td colSpan={3}>
                {t(
                  'onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings',
                )}
              </td>
            </tr>
          ) : (
            mappings.map((mapping, index) => (
              <tr key={`${mapping.sourceCode}-${index}`}>
                <td>{mapping.sourceDisplay || '—'}</td>
                <td>{mapping.sourceCode}</td>
                <td>{mapping.hslocCode}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  
    <AsyncStatus loading={loading} error={error} />

    {!loading &&
      !error &&
      hslocUnmappedCodes.length > 0 && (
        <>
          <h3 id={acquiredValueHeadingId} className="nhsn-link__report-results-detail-section-title">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.acquiredValueHeading',
            )}
          </h3>
          {dataLoading ? (
            <NHSNLoadingIndicator />
          ) : (
            <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
              <table className="nhsn-link__report-results-table" aria-labelledby={acquiredValueHeadingId}>
                <thead>
                  <tr>
                    <th scope="col">
                      {vendorProfile?.hslocSourceLabel ??
                        t(
                          'onboarding:hsloc.mapping.fields.locationValueFallback',
                        )}
                    </th>
                    <th scope="col">
                      <AcronymText>
                        {t(
                          'onboarding:reportResults.detail.mappingEvidence.hslocCode',
                        )}
                      </AcronymText>
                    </th>
                    <th aria-hidden="true" />
                  </tr>
                </thead>
                <tbody>
                  {hslocUnmappedCodes.map((code) => (
                    <tr
                      key={code}
                      className="nhsn-link__report-results-row--unmapped">
                      <td>{code}</td>
                      <td>
                        <Select
                          id={`hsloc-add-${code}`}
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
                          value={selections[code] ?? ''}
                          onChange={(value) => onSelectionChange(code, value)}
                        />
                      </td>
                      <td>
                        <Button
                          variant="secondary"
                          onClick={() => onAddMapping(code)}
                          disabled={
                            !selections[code] ||
                            addingCode === code
                          }
                          loading={addingCode === code}>
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
          )}
          <p className="nhsn-link__report-results-hint-box">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.hslocAddedHint',
            )}
          </p>
        </>
      )}
  </Modal>
  );
}

export default HslocEvidenceModal;
