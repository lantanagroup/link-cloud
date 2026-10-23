import React, { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { LocationMethod, PatientMappingEvidence } from '../../../api/contracts';
import { acronymTitle, Button, HeadingPause, Modal, MessageContainer } from '../../../fields';
import { useOnboarding } from '../../OnboardingProvider';
import { METHOD_LABEL_KEYS } from '../location-org/LocationOrgStep';
import { AsyncStatus } from './AsyncStatus';
import type { LocationDiscoveryRow, LocationRawValues } from './locationOrgDiscovery';
import { locationOrgConfigInfo, type PatientStatusRow } from './patientRows';

function formatRawValue(
  method: LocationMethod | undefined,
  raw: LocationRawValues | null,
): string | null {
  if (!raw) {
    return null;
  }
  if (method === 'location-type') {
    const codes = raw.locationTypeCodes.join(', ');
    return [codes, raw.alias].filter(Boolean).join(' / ') || null;
  }
  if (method === 'location-identifier') {
    return raw.identifiers.length > 0
      ? raw.identifiers.map((identifier) => `${identifier.system ?? ''}: ${identifier.value ?? ''}`).join('; ')
      : null;
  }
  if (method === 'managing-org') {
    return raw.managingOrganizationId ?? null;
  }
  return null;
}

interface EvidenceRow {
  key: string;
  locationId: string;
  rawValue: string | null;
  found: boolean;
}

function evidenceRows(
  discoveryRows: LocationDiscoveryRow[] | null,
  evidence: PatientMappingEvidence | null,
  method: LocationMethod | undefined,
): EvidenceRow[] | null {
  if (discoveryRows) {
    return discoveryRows.map((row) => ({
      key: row.locationId,
      locationId: row.locationId,
      rawValue: formatRawValue(method, row.raw),
      found: row.found,
    }));
  }
  if (evidence === null) {
    return null;
  }
  return (evidence.locationOrg?.matches ?? []).map((match, index) => ({
    key: `${match.locationId}-${index}`,
    locationId: match.locationId,
    rawValue: match.locationAlias ?? match.locationName ?? null,
    found: match.isOrgLocation,
  }));
}

export interface LocationOrgEvidenceModalProps {
  open: boolean;
  onClose: () => void;
  patientId: string | null;
  patientRow: PatientStatusRow | null;
  loading: boolean;
  error: string | null;
  evidence: PatientMappingEvidence | null;
  discoveryRows: LocationDiscoveryRow[] | null;
  notReportable: boolean;
}

export function LocationOrgEvidenceModal({
  open,
  onClose,
  patientId,
  patientRow,
  loading,
  error,
  evidence,
  discoveryRows,
  notReportable,
}: LocationOrgEvidenceModalProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const { draft, goTo } = useOnboarding();
  const locationOrgConfig = locationOrgConfigInfo(draft.locationOrg, t);
  const notFoundHintId = useId();
  const rows = evidenceRows(discoveryRows, evidence, draft.locationOrg.method);

  return (
  <Modal
    open={open}
    title={acronymTitle(
      <HeadingPause>{t('onboarding:reportResults.detail.mappingEvidence.locationOrgTitle')}</HeadingPause>,
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
    {notReportable && (
      <MessageContainer type="info" showIcon>
        <p>
          {t(
            'onboarding:reportResults.detail.mappingEvidence.notReportableHint',
          )}
        </p>
      </MessageContainer>
    )}
    {!loading && !error && rows && (
      rows.length > 0 ? (
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
                    'onboarding:reportResults.detail.mappingEvidence.discoveryLocationId',
                  )}
                </th>
                <th scope="col">
                  {t(
                    'onboarding:reportResults.detail.mappingEvidence.discoveryRawValue',
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
              {rows.map((row) => (
                <tr key={row.key}>
                  <td>{row.locationId}</td>
                  <td>
                    {row.rawValue ??
                      t(
                        'onboarding:reportResults.detail.mappingEvidence.discoveryNotAvailable',
                      )}
                  </td>
                  <td>
                    <span
                      className={`nhsn-link__mapping-pill ${row.found ? 'nhsn-link__mapping-pill--found' : 'nhsn-link__mapping-pill--not-found'}`}>
                      {row.found
                        ? t('onboarding:reportResults.detail.mapping.found')
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
          {t('onboarding:reportResults.detail.mappingEvidence.noEvidence')}
        </p>
      )
    )}
  
    {patientRow &&
      !patientRow.locationOrgFound && (
        <MessageContainer type="info" showIcon>
          <p id={notFoundHintId} role="status">
            {t(
              'onboarding:reportResults.detail.mappingEvidence.notFoundLocationOrgHint',
            )}
          </p>
          <Button
            variant="secondary"
            aria-describedby={notFoundHintId}
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
