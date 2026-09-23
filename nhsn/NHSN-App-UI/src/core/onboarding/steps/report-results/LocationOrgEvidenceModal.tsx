import React from 'react';
import { useTranslation } from 'react-i18next';
import type { PatientMappingEvidence } from '../../../api/contracts';
import { Button, Modal, MessageContainer } from '../../../fields';
import { useOnboarding } from '../../OnboardingProvider';
import { METHOD_LABEL_KEYS } from '../location-org/LocationOrgStep';
import { AsyncStatus } from './AsyncStatus';
import { locationOrgConfigInfo, type PatientStatusRow } from './patientRows';

export interface LocationOrgEvidenceModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string | null;
  patientRow: PatientStatusRow | null;
  loading: boolean;
  error: string | null;
  evidence: PatientMappingEvidence | null;
}

export function LocationOrgEvidenceModal({
  open,
  onClose,
  patientId,
  patientRow,
  loading,
  error,
  evidence,
}: LocationOrgEvidenceModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const { draft, goTo } = useOnboarding();
  const locationOrgConfig = locationOrgConfigInfo(draft.locationOrg, t);

  return (
  <Modal
    open={open}
    title={t(
      'onboarding:reportResults.detail.mappingEvidence.locationOrgTitle',
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
      <div>
        <dt>
          {t(
            'onboarding:reportResults.detail.mappingEvidence.resolutionMethod',
          )}
        </dt>
        <dd>
          {locationOrgConfig
            ? t(METHOD_LABEL_KEYS[locationOrgConfig.method])
            : t('onboarding:reportResults.detail.notApplicable')}
        </dd>
      </div>
    </dl>
  
    <h3 className="nhsn-link__report-results-detail-section-title">
      {t(
        'onboarding:reportResults.detail.mappingEvidence.configuredLocationOrgMappings',
      )}
    </h3>
    {locationOrgConfig ? (
      <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
        <table className="nhsn-link__report-results-table">
          <caption className="nhsn-link__visually-hidden">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.configuredLocationOrgMappings',
            )}
          </caption>
          <thead>
            <tr>
              {locationOrgConfig.headers.map((header) => (
                <th key={header} scope="col">
                  {header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {locationOrgConfig.rows.length === 0 ? (
              <tr>
                <td colSpan={locationOrgConfig.headers.length}>
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.noConfiguredMappings',
                  )}
                </td>
              </tr>
            ) : (
              locationOrgConfig.rows.map((cells, index) => (
                <tr key={index}>
                  {cells.map((cell, cellIndex) => (
                    <td key={cellIndex}>{cell || '—'}</td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    ) : (
      <p className="nhsn-link__hint-text">
        {t(
          'onboarding:reportResults.detail.mappingEvidence.noStructuredMethod',
        )}
      </p>
    )}
  
    <h3 className="nhsn-link__report-results-detail-section-title">
      {t(
        'onboarding:reportResults.detail.mappingEvidence.locationEvidenceHeading',
      )}
    </h3>
    <AsyncStatus loading={loading} error={error} />
    {!loading &&
      !error &&
      evidence &&
      (evidence.locationOrg &&
      evidence.locationOrg.matches.length > 0 ? (
        <div className="nhsn-link__report-results-table-scroll" tabIndex={-1}>
          <table className="nhsn-link__report-results-table">
            <caption className="nhsn-link__visually-hidden">
              {t(
                'onboarding:reportResults.detail.mappingEvidence.locationEvidenceHeading',
              )}
            </caption>
            <thead>
              <tr>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.locationId',
                  )}
                </th>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.locationAlias',
                  )}
                </th>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.isOrgLocation',
                  )}
                </th>
              </tr>
            </thead>
            <tbody>
              {evidence.locationOrg.matches.map((match, index) => (
                <tr key={`${match.locationId}-${index}`}>
                  <td>{match.locationId}</td>
                  <td>
                    {match.locationAlias ?? match.locationName ?? '—'}
                  </td>
                  <td>
                    <span
                      className={`nhsn-link__mapping-pill ${match.isOrgLocation ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                      {match.isOrgLocation
                        ? t(
                            'onboarding:reportResults.detail.mapping.found',
                          )
                        : t(
                            'onboarding:reportResults.detail.mapping.notFound',
                          )}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p>
          {t(
            'onboarding:reportResults.detail.mappingEvidence.noEvidence',
          )}
        </p>
      ))}
  
    {patientRow &&
      !patientRow.locationOrgFound && (
        <MessageContainer type="info" showIcon>
          <p role="status">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.notFoundLocationOrgHint',
            )}
          </p>
          <Button
            variant="secondary"
            onClick={() => {
              onClose();
              goTo('location-org');
            }}>
            {t(
              'onboarding:reportResults.detail.mappingEvidence.goToLocationOrg',
            )}
          </Button>
        </MessageContainer>
      )}
  </Modal>
  );
}

export default LocationOrgEvidenceModal;
