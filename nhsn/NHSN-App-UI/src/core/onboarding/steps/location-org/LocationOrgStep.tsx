import React, {useEffect, useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {LocationCandidate, LocationMethod} from '../../../api/contracts';
import {InstructionsDownload} from '../../../documents';
import {
  Button,
  FieldLabel,
  InlineSpinner,
  Modal,
  RepeatableList,
  StepActions,
  Tabs,
  TextField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import type {LocationIdentifierEntry, LocationTypeEntry} from '../../types';
import {findIncompleteLocationIdentifierIndexes, findIncompleteLocationTypeIndexes} from './validate';

/** Organization Identification. Methods and instructions PDF both come from `vendorProfile` - no vendor name here. */
export function LocationOrgStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError, notifySuccess} = useNotifications();
  const {draft, patch, saving, savingDirection, vendorProfile} = useOnboarding();

  const locationOrg = draft.locationOrg;
  const methods = vendorProfile?.locationMethods ?? [];
  const vendorName = vendorProfile?.displayName ?? '';
  const instructionsKey = vendorProfile?.documentKeys.locationOrgResolution;

  // A method the profile no longer offers selects nothing.
  const activeMethod =
    locationOrg.method && methods.includes(locationOrg.method) ? locationOrg.method : undefined;

  // Nothing saved yet - either no one has picked a method online, or an imported sheet's method
  // didn't parse into one the vendor profile recognizes. Rather than land on no tab at all, default
  // to the vendor's first method - unless a custom FHIRPath was actually entered with no location
  // identifiers alongside it, in which case that's the tab with real data to show.
  useEffect(() => {
    if (activeMethod || methods.length === 0) {
      return;
    }
    const hasCustomFhirPath = Boolean(locationOrg.customFhirPath?.trim());
    const hasLocationIdentifiers = (locationOrg.locationIdentifiers?.length ?? 0) > 0;
    const defaultMethod =
      hasCustomFhirPath && !hasLocationIdentifiers && methods.includes('custom-fhir-path')
        ? 'custom-fhir-path'
        : methods[0];
    patch('locationOrg', {method: defaultMethod});
  }, [activeMethod, methods, locationOrg.customFhirPath, locationOrg.locationIdentifiers, patch]);

  const [searchOpen, setSearchOpen] = useState(false);
  const [searching, setSearching] = useState(false);
  const [candidates, setCandidates] = useState<LocationCandidate[]>([]);
  const [selectedCandidates, setSelectedCandidates] = useState<Record<string, boolean>>({});

  const managingOrganizations = locationOrg.managingOrganizationIds ?? [];
  const locationTypes = locationOrg.locationTypes ?? [];
  const locationIdentifiers = locationOrg.locationIdentifiers ?? [];

  // Only the active method's rows can block Continue - a different method's rows just sit hidden
  // in the draft, unrelated to what's being configured right now (same reasoning as `activeMethod`
  // itself only ever showing one method's fields at a time).
  const incompleteLocationTypeIndexes = useMemo(
    () => new Set(findIncompleteLocationTypeIndexes(locationTypes)),
    [locationTypes]
  );
  const incompleteLocationIdentifierIndexes = useMemo(
    () => new Set(findIncompleteLocationIdentifierIndexes(locationIdentifiers)),
    [locationIdentifiers]
  );
  const hasIncompleteRows =
    (activeMethod === 'location-type' && incompleteLocationTypeIndexes.size > 0) ||
    (activeMethod === 'location-identifier' && incompleteLocationIdentifierIndexes.size > 0);

  function handleMethodChange(method: LocationMethod) {
    patch('locationOrg', {method});
  }

  async function handleSearch() {
    setSelectedCandidates({});
    setCandidates([]);
    setSearchOpen(true);
    setSearching(true);
    try {
      setCandidates(await api.getLocationCandidates('location-type'));
    } catch (cause) {
      setSearchOpen(false);
      notifyError(
        cause instanceof Error ? cause.message : t('onboarding:locationOrg.messages.searchError')
      );
    } finally {
      setSearching(false);
    }
  }

  function handleAddSelected() {
    const chosen = candidates.filter(candidate => selectedCandidates[candidate.id]);
    if (chosen.length === 0) {
      return;
    }
    patch('locationOrg', {
      locationTypes: [
        ...locationTypes,
        ...chosen.map(candidate => ({code: candidate.typeCodings?.[0]?.code ?? '', alias: candidate.display}))
      ]
    });
    setSearchOpen(false);
    notifySuccess(t('onboarding:locationOrg.messages.candidatesAdded', {count: chosen.length}));
  }

  const anySelected = candidates.some(candidate => selectedCandidates[candidate.id]);

  const stableOnBack = useStableCallback(onBack);
  const stableOnNext = useStableCallback(onNext);

  useStepChrome(
    useMemo(
      () => ({
        title: t('onboarding:locationOrg.title'),
        footer: (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={stableOnBack} disabled={saving} loading={savingDirection === 'back'}>
              {t('common:actions.back')}
            </Button>
            <Button onClick={stableOnNext} disabled={saving || hasIncompleteRows} loading={savingDirection === 'next'}>
              {t('common:actions.continue')}
            </Button>
          </StepActions>
        )
      }),
      [t, saving, savingDirection, stableOnBack, stableOnNext, hasIncompleteRows]
    )
  );

  return (
    <div className="nhsn-link__location-org">
      <p className="nhsn-link__subtitle">{t('onboarding:locationOrg.intro')}</p>

      {methods.length > 0 && (
        <div className="nhsn-link__field-group">
          <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.methodTooltip')}>{t('onboarding:locationOrg.methodLabel')}</FieldLabel>
          <Tabs<LocationMethod>
            label={t('onboarding:locationOrg.methodLabel')}
            tabs={methods.map(method => ({id: method, label: t(METHOD_LABEL_KEYS[method])}))}
            activeTab={activeMethod}
            onTabChange={handleMethodChange}
          />
        </div>
      )}

      {activeMethod === 'location-type' && (
        <>
          <p className="nhsn-link__subtitle">
            {t('onboarding:locationOrg.locationType.intro')}
          </p>
          <div className="nhsn-link__field-group">
            <Button variant="secondary" onClick={handleSearch}>
              {t('onboarding:locationOrg.locationType.search', {vendor: vendorName})}
            </Button>
          </div>

          <div className="nhsn-link__field-group">
            <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.locationType.tooltip')}>{t('onboarding:locationOrg.locationType.listLabel')}</FieldLabel>
            <RepeatableList<LocationTypeEntry>
              items={locationTypes}
              onChange={rows => patch('locationOrg', {locationTypes: rows})}
              newItem={() => ({code: '', alias: ''})}
              addLabel={t('onboarding:locationOrg.locationType.add')}
              removeLabel={t('common:actions.remove')}
              emptyLabel={t('onboarding:locationOrg.noneAdded')}
              renderItem={(row, index, onRowChange) => {
                // Only flag a half-filled row (one side typed, the other still blank) - a freshly
                // added, entirely untouched row isn't wrong yet, just unfinished, so it stays quiet
                // until the facility actually starts it.
                const partial = Boolean(row.code.trim()) !== Boolean(row.alias.trim());
                return (
                  <>
                    <TextField
                      id={`location-type-code-${index}`}
                      label={t('onboarding:locationOrg.locationType.codeLabel')}
                      placeholder={t('onboarding:locationOrg.locationType.codeLabel')}
                      value={row.code}
                      error={partial && !row.code.trim() ? t('onboarding:locationOrg.errors.rowIncomplete') : undefined}
                      onChange={code => onRowChange({...row, code})}
                    />
                    <TextField
                      id={`location-type-alias-${index}`}
                      label={t('onboarding:locationOrg.locationType.aliasLabel')}
                      placeholder={t('onboarding:locationOrg.locationType.aliasLabel')}
                      value={row.alias}
                      error={partial && !row.alias.trim() ? t('onboarding:locationOrg.errors.rowIncomplete') : undefined}
                      onChange={alias => onRowChange({...row, alias})}
                    />
                  </>
                );
              }}
            />
          </div>
        </>
      )}

      {activeMethod === 'managing-org' && (
        <div className="nhsn-link__field-group">
          <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.managingOrg.tooltip')}>{t('onboarding:locationOrg.managingOrg.listLabel')}</FieldLabel>
          <RepeatableList<string>
            items={managingOrganizations}
            onChange={rows => patch('locationOrg', {managingOrganizationIds: rows})}
            newItem={() => ''}
            addLabel={t('onboarding:locationOrg.managingOrg.add')}
            removeLabel={t('common:actions.remove')}
            emptyLabel={t('onboarding:locationOrg.noneAdded')}
            renderItem={(row, index, onRowChange) => (
              <TextField
                id={`managing-org-${index}`}
                label={t('onboarding:locationOrg.managingOrg.listLabel')}
                placeholder={t('onboarding:locationOrg.managingOrg.placeholder')}
                value={row}
                onChange={onRowChange}
              />
            )}
          />
        </div>
      )}

      {activeMethod === 'location-identifier' && (
        <>
          {instructionsKey && (
            <InstructionsDownload
              href={api.getLocationOrgResolutionUrl()}
              description={t('onboarding:locationOrg.locationIdentifier.instructions')}
              linkText={t('onboarding:locationOrg.locationIdentifier.downloadPdf')}
            />
          )}

          <div className="nhsn-link__field-group">
            <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.locationIdentifier.tooltip')}>{t('onboarding:locationOrg.locationIdentifier.listLabel')}</FieldLabel>
            <RepeatableList<LocationIdentifierEntry>
              items={locationIdentifiers}
              onChange={rows => patch('locationOrg', {locationIdentifiers: rows})}
              newItem={() => ({system: '', code: ''})}
              addLabel={t('onboarding:locationOrg.locationIdentifier.add')}
              removeLabel={t('common:actions.remove')}
              emptyLabel={t('onboarding:locationOrg.noneAdded')}
              renderItem={(row, index, onRowChange) => {
                const partial = Boolean(row.system.trim()) !== Boolean(row.code.trim());
                return (
                  <>
                    <TextField
                      id={`location-identifier-system-${index}`}
                      label={t('onboarding:locationOrg.locationIdentifier.systemLabel')}
                      placeholder={t('onboarding:locationOrg.locationIdentifier.systemLabel')}
                      value={row.system}
                      error={partial && !row.system.trim() ? t('onboarding:locationOrg.errors.rowIncomplete') : undefined}
                      onChange={system => onRowChange({...row, system})}
                    />
                    <TextField
                      id={`location-identifier-code-${index}`}
                      label={t('onboarding:locationOrg.locationIdentifier.codeLabel')}
                      placeholder={t('onboarding:locationOrg.locationIdentifier.codeLabel')}
                      value={row.code}
                      error={partial && !row.code.trim() ? t('onboarding:locationOrg.errors.rowIncomplete') : undefined}
                      onChange={code => onRowChange({...row, code})}
                    />
                  </>
                );
              }}
            />
          </div>
        </>
      )}

      {(activeMethod === 'custom-fhir-path' || methods.length === 0) && (
        <div className="nhsn-link__field-group nhsn-link__field-group--labeled">
          <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.customFhirPath.tooltip')}>{t('onboarding:locationOrg.customFhirPath.label')}</FieldLabel>
          <TextField
            id="custom-fhir-path"
            label={t('onboarding:locationOrg.customFhirPath.label')}
            placeholder={t('onboarding:locationOrg.customFhirPath.placeholder')}
            value={locationOrg.customFhirPath ?? ''}
            onChange={customFhirPath => patch('locationOrg', {customFhirPath})}
          />
        </div>
      )}

      <Modal
        open={searchOpen}
        title={t('onboarding:locationOrg.locationType.searchTitle', {vendor: vendorName})}
        onClose={() => setSearchOpen(false)}
        size="large"
        footer={
          <>
            <Button variant="secondary" onClick={() => setSearchOpen(false)}>
              {t('common:actions.cancel')}
            </Button>
            <Button onClick={handleAddSelected} disabled={searching || !anySelected}>
              {t('onboarding:locationOrg.locationType.addSelected')}
            </Button>
          </>
        }>
        {searching ? (
          <InlineSpinner label={t('onboarding:locationOrg.locationType.searching')} />
        ) : (
          <CandidateTable
            caption={t('onboarding:locationOrg.locationType.searchTitle', {vendor: vendorName})}
            candidates={candidates}
            selected={selectedCandidates}
            onToggle={(id, checked) => setSelectedCandidates(current => ({...current, [id]: checked}))}
            columnLabels={{
              id: t('onboarding:locationOrg.locationType.columns.id'),
              alias: t('onboarding:locationOrg.locationType.columns.alias'),
              type: t('onboarding:locationOrg.locationType.columns.type'),
              codings: t('onboarding:locationOrg.locationType.columns.codings')
            }}
            emptyLabel={t('onboarding:locationOrg.locationType.noCandidates')}
          />
        )}
      </Modal>

      {hasIncompleteRows && (
        <p className="nhsn-link__form-error" role="alert">
          {t('onboarding:locationOrg.errors.incompleteRows')}
        </p>
      )}
    </div>
  );
}

export default LocationOrgStep;

/**
 * i18n key per method, so no kebab-case identifier is built by string concatenation. Exported for
 * the Report Details "Location Org Mapping" modal, which shows the same method label read-only.
 */
export const METHOD_LABEL_KEYS: Record<LocationMethod, string> = {
  'managing-org': 'onboarding:locationOrg.methods.managingOrg',
  'location-identifier': 'onboarding:locationOrg.methods.locationIdentifier',
  'location-type': 'onboarding:locationOrg.methods.locationType',
  'custom-fhir-path': 'onboarding:locationOrg.methods.customFhirPath'
};

interface CandidateTableProps {
  candidates: LocationCandidate[];
  selected: Record<string, boolean>;
  onToggle: (id: string, checked: boolean) => void;
  columnLabels: {id: string; alias: string; type: string; codings: string};
  emptyLabel: string;
  caption: string;
}

/** Multi-select over search results. Columns match the POC's Cerner site-location table. */
function CandidateTable({candidates, selected, onToggle, columnLabels, emptyLabel, caption}: CandidateTableProps) {
  if (candidates.length === 0) {
    return <p className="nhsn-link__hint-text">{emptyLabel}</p>;
  }

  return (
    <div className="nhsn-link__table-scroll" tabIndex={-1}>
      <table className="nhsn-link__table">
        <caption className="nhsn-link__visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col" className="nhsn-link__table-select-column" />
            <th scope="col">{columnLabels.id}</th>
            <th scope="col">{columnLabels.alias}</th>
            <th scope="col">{columnLabels.type}</th>
            <th scope="col">{columnLabels.codings}</th>
          </tr>
        </thead>
        <tbody>
          {candidates.map(candidate => (
            <tr key={candidate.id}>
              <td>
                <input
                  type="checkbox"
                  checked={Boolean(selected[candidate.id])}
                  aria-label={candidate.display}
                  onChange={event => onToggle(candidate.id, event.target.checked)}
                />
              </td>
              <td>{candidate.id}</td>
              <td>{candidate.display}</td>
              <td>{candidate.typeText ?? '—'}</td>
              <td>
                {candidate.typeCodings?.length ? (
                  <ul className="nhsn-link__plain-list">
                    {candidate.typeCodings.map((coding, index) => (
                      <li key={index}>
                        code: {coding.code ?? '—'}, display: "{coding.display ?? '—'}"
                      </li>
                    ))}
                  </ul>
                ) : (
                  '—'
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
