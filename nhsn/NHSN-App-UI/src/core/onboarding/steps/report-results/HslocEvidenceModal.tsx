import React from 'react';
import { useTranslation } from 'react-i18next';
import type {
  HslocCode,
  HslocMapping,
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

  const hslocUnmappedCodes = evidence
    ? Array.from(
        new Set(
          evidence.codeMaps
            .filter(isHslocCodeMap)
            .flatMap((codeMap) => codeMap.unmappedCodes),
        ),
      )
    : [];

  return (
  <Modal
    open={open}
    title={t(
      'onboarding:reportResults.detail.mappingEvidence.hslocTitle',
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
  
    <h3 className="nhsn-link__report-results-detail-section-title">
      {t(
        'onboarding:reportResults.detail.mappingEvidence.configuredHslocMappings',
      )}
    </h3>
    <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
      <table className="nhsn-link__report-results-table">
        <caption className="nhsn-link__visually-hidden">
          {t(
            'onboarding:reportResults.detail.mappingEvidence.configuredHslocMappings',
          )}
        </caption>
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
              {t(
                'onboarding:reportResults.detail.mappingEvidence.hslocCode',
              )}
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
  
    <p className="nhsn-link__visually-hidden" role="alert">
      {!loading ? error : null}
    </p>
    {loading && <NHSNLoadingIndicator />}
    {!loading && error && (
      <MessageContainer type="error" showIcon>
        <span>{error}</span>
      </MessageContainer>
    )}
  
    {!loading &&
      !error &&
      hslocUnmappedCodes.length > 0 && (
        <>
          <h3 className="nhsn-link__report-results-detail-section-title">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.acquiredValueHeading',
            )}
          </h3>
          {dataLoading ? (
            <NHSNLoadingIndicator />
          ) : (
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
                      {vendorProfile?.hslocSourceLabel ??
                        t(
                          'onboarding:hsloc.mapping.fields.locationValueFallback',
                        )}
                    </th>
                    <th scope="col">
                      {t(
                        'onboarding:reportResults.detail.mappingEvidence.hslocCode',
                      )}
                    </th>
                    <th aria-hidden="true" />
                  </tr>
                </thead>
                <tbody>
                  {hslocUnmappedCodes.map((code) => (
                    <tr key={code}>
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
          <p className="nhsn-link__hint-text">
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
