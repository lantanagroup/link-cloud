import React, {useEffect, useMemo, useRef, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {LocationCandidate, LocationMethod} from '../../../api/contracts';
import {InstructionsDownload} from '../../../documents';
import {
  acronymTitle,
  Button,
  FieldLabel,
  HeadingPause,
  InlineSpinner,
  Modal,
  RepeatableList,
  StepActions,
  Tabs,
  TextField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding, useStepValidator} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import type {LocationIdentifierEntry, LocationOrgDraft, LocationTypeEntry} from '../../types';
import {
  findIncompleteLocationIdentifierIndexes,
  findIncompleteLocationTypeIndexes,
  isPlausibleFhirPath
} from './validate';

/** Organization Identification. Methods and instructions PDF both come from `vendorProfile` - no vendor name here. */
export function LocationOrgStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError, notifySuccess} = useNotifications();
  const {draft, patch, mirror, saving, savingDirection, vendorProfile} = useOnboarding();

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
    mirror('locationOrg', {method: defaultMethod});
  }, [activeMethod, methods, locationOrg.customFhirPath, locationOrg.locationIdentifiers, mirror]);

  const [searchOpen, setSearchOpen] = useState(false);
  const [searching, setSearching] = useState(false);
  const [candidates, setCandidates] = useState<LocationCandidate[]>([]);
  const [selectedCandidates, setSelectedCandidates] = useState<Record<string, boolean>>({});

  // No forced starting row - an empty list stays empty until the facility clicks "+ Add", same as
  // every RepeatableList's own minItems={0} below lets it go back to zero after that.
  const managingOrganizations = locationOrg.managingOrganizationIds ?? [];
  const locationTypes = locationOrg.locationTypes ?? [];
  const locationIdentifiers = locationOrg.locationIdentifiers ?? [];

  // Only the active method's rows can block Continue - a different method's rows just sit hidden
  // in the draft, unrelated to what's being configured right now (same reasoning as `activeMethod`
  // itself only ever showing one method's fields at a time).
  const incompleteRowIndexes =
    activeMethod === 'location-type'
      ? findIncompleteLocationTypeIndexes(locationTypes)
      : activeMethod === 'location-identifier'
        ? findIncompleteLocationIdentifierIndexes(locationIdentifiers)
        : activeMethod === 'managing-org'
          ? managingOrganizations.flatMap((id, index) => (id.trim() ? [] : [index]))
          : [];
  const activeRowCount =
    activeMethod === 'location-type'
      ? locationTypes.length
      : activeMethod === 'location-identifier'
        ? locationIdentifiers.length
        : activeMethod === 'managing-org'
          ? managingOrganizations.length
          : 0;

  // Zero rows is fine - a method can be selected with nothing added yet, same as the manual-upload
  // import path. But once a row EXISTS (the facility clicked "+ Add", even if they haven't typed
  // anything into it yet), it has to be either filled in or removed before Continue - a row just
  // sitting there empty is exactly as ambiguous as a half-filled one: incompleteRowIndexes already
  // flags a fully-blank row the same way it flags a half-filled one, and with zero rows it's
  // naturally empty, so no separate "is everything blank" check is needed here.
  const hasIncompleteRows = incompleteRowIndexes.length > 0;

  // Only the Custom FHIRPath tab has a free-form expression to check; Data Acquisition compiles it
  // on save, and without this a typo only surfaces as a bare "DataAcquisition returned 400".
  const customFhirPath = locationOrg.customFhirPath ?? '';
  const hasInvalidFhirPath = activeMethod === 'custom-fhir-path' && !isPlausibleFhirPath(customFhirPath);
  const [fhirPathBlurred, setFhirPathBlurred] = useState(false);

  // No error is worth showing before the facility has actually tried to move on - an untouched,
  // still-blank list isn't wrong yet, just not started. Once shown, though, it tracks the live state
  // below, so fixing (or re-breaking) it updates the errors without another click.
  //
  // Kept per method: the value is how many rows existed at that method's last Continue attempt -
  // only these get "required" errors on their blank fields, so a row added afterwards stays quiet
  // until the next attempt instead of showing up already in error. Switching tabs and coming back
  // doesn't clear it - the rows are still just as incomplete, so their errors come back with them.
  const [flaggedRowCounts, setFlaggedRowCounts] = useState<Partial<Record<LocationMethod, number>>>({});
  const continueAttempted = activeMethod !== undefined && flaggedRowCounts[activeMethod] !== undefined;
  const flaggedRowCount = (activeMethod && flaggedRowCounts[activeMethod]) ?? 0;
  const isRowFlagged = (index: number) => index < flaggedRowCount;
  const hasFlaggedIncompleteRows = hasIncompleteRows && incompleteRowIndexes.some(isRowFlagged);
  const showFhirPathError = hasInvalidFhirPath && (fhirPathBlurred || continueAttempted);

  function validateStep(): boolean {
    if (activeMethod) {
      setFlaggedRowCounts(counts => ({...counts, [activeMethod]: activeRowCount}));
    }
    setFhirPathBlurred(true);
    return !hasIncompleteRows && !hasInvalidFhirPath;
  }

  /** Keeps the active method's flagged count pointing at the same rows when one of them is removed. */
  function trackRemoval<T>(previous: T[], next: T[]) {
    if (!activeMethod || next.length >= previous.length) {
      return;
    }
    const removedIndex = previous.findIndex((row, index) => row !== next[index]);
    setFlaggedRowCounts(counts => {
      const count = counts[activeMethod];
      return count !== undefined && removedIndex < count ? {...counts, [activeMethod]: count - 1} : counts;
    });
  }

  // Advancing is deferred a render when inactive tabs were just cleared, so that patch is in the
  // draft the provider saves - `onNext` saves the draft it was rendered with (same as FhirStep).
  const [readyToAdvance, setReadyToAdvance] = useState(false);
  useEffect(() => {
    if (readyToAdvance) {
      setReadyToAdvance(false);
      onNext();
    }
  }, [readyToAdvance, onNext]);

  /**
   * Only the active method is ever saved - the BFF turns it alone into Data Acquisition's
   * conditions and rebuilds this section from those on the next load - so whatever sits in the
   * other tabs is unsaved and would otherwise linger in memory (an abandoned incomplete row coming
   * back on the next visit). Continuing drops it, leaving the draft matching what was saved.
   * Limited to the tabs this vendor offers: a method only the other vendor offers is hidden, not
   * abandoned, so switching vendor never costs that data.
   */
  function discardInactiveTabs(): boolean {
    const cleared: Partial<LocationOrgDraft> = {};
    methods.forEach(method => {
      const field = METHOD_FIELDS[method];
      const value = locationOrg[field];
      if (method !== activeMethod && (Array.isArray(value) ? value.length > 0 : Boolean(value))) {
        Object.assign(cleared, {[field]: field === 'customFhirPath' ? undefined : []});
      }
    });
    if (Object.keys(cleared).length === 0) {
      return false;
    }
    patch('locationOrg', cleared);
    return true;
  }

  function handleContinue() {
    if (!validateStep()) {
      return;
    }
    if (discardInactiveTabs()) {
      setReadyToAdvance(true);
    } else {
      onNext();
    }
  }

  useStepValidator(validateStep);

  function requiredError(field: string): string {
    return t('onboarding:locationOrg.errors.fieldRequiredError', {field});
  }

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

  // A search result already in the Location Type list (same code and alias) isn't offered again -
  // removing that row from the list brings it back on the next search.
  const addedLocationTypeKeys = new Set(locationTypes.map(row => locationTypeKey(row.code, row.alias)));
  const availableCandidates = candidates.filter(
    candidate => !addedLocationTypeKeys.has(locationTypeKey(candidateTypeCode(candidate), candidate.display))
  );

  function handleAddSelected() {
    const chosen = availableCandidates.filter(candidate => selectedCandidates[candidate.id]);
    if (chosen.length === 0) {
      return;
    }
    patch('locationOrg', {
      locationTypes: [
        // Drops the untouched placeholder row (or any other left fully blank) the search replaces.
        ...locationTypes.filter(row => row.code.trim() || row.alias.trim()),
        ...chosen.map(candidate => ({code: candidateTypeCode(candidate), alias: candidate.display}))
      ]
    });
    setSearchOpen(false);
    notifySuccess(t('onboarding:locationOrg.messages.candidatesAdded', {count: chosen.length}));
  }

  const anySelected = availableCandidates.some(candidate => selectedCandidates[candidate.id]);

  const stableOnBack = useStableCallback(onBack);
  const stableHandleContinue = useStableCallback(handleContinue);

  useStepChrome(
    useMemo(
      () => ({
        title: acronymTitle(<HeadingPause>{t('onboarding:locationOrg.title')}</HeadingPause>),
        footer: (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={stableOnBack} disabled={saving} loading={savingDirection === 'back'}>
              {t('common:actions.back')}
            </Button>
            <Button onClick={stableHandleContinue} disabled={saving} loading={savingDirection === 'next'}>
              {t('common:actions.continue')}
            </Button>
          </StepActions>
        )
      }),
      [t, saving, savingDirection, stableOnBack, stableHandleContinue]
    )
  );

  const customFhirPathContent = (
    <div className="nhsn-link__field-group nhsn-link__field-group--labeled">
      <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.customFhirPath.tooltip')}>{t('onboarding:locationOrg.customFhirPath.label')}</FieldLabel>
      <TextField
        id="custom-fhir-path"
        label={t('onboarding:locationOrg.customFhirPath.label')}
        placeholder={t('onboarding:locationOrg.customFhirPath.placeholder')}
        value={customFhirPath}
        error={showFhirPathError ? t('onboarding:locationOrg.errors.invalidFhirPath') : undefined}
        onChange={value => patch('locationOrg', {customFhirPath: value})}
        onBlur={() => setFhirPathBlurred(true)}
      />
    </div>
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
            onTabChange={handleMethodChange}>
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
            <RepeatableList<LocationTypeEntry>
              items={locationTypes}
              onChange={rows => {
                trackRemoval(locationTypes, rows);
                patch('locationOrg', {locationTypes: rows});
              }}
              newItem={() => ({code: '', alias: ''})}
              minItems={0}
              addLabel={t('onboarding:locationOrg.locationType.add')}
              removeLabel={t('common:actions.remove')}
              columnHeadings={[
                t('onboarding:locationOrg.locationIdentifier.systemPlaceholder'),
                t('onboarding:locationOrg.locationIdentifier.codePlaceholder')
              ]}
              renderItem={(row, index, onRowChange) => {
                // Blank fields are only flagged once Continue has been tried with this row present - not
                // while the facility is still filling it in.
                const flagBlank = isRowFlagged(index);
                return (
                  <>
                    <TextField
                      id={`location-type-code-${index}`}
                      label={t('onboarding:locationOrg.locationType.codeLabel')}
                      value={row.code}
                      error={
                        flagBlank && !row.code.trim()
                          ? requiredError(t('onboarding:locationOrg.locationType.codeLabel'))
                          : undefined
                      }
                      onChange={code => onRowChange({...row, code})}
                    />
                    <TextField
                      id={`location-type-alias-${index}`}
                      label={t('onboarding:locationOrg.locationType.aliasLabel')}
                      value={row.alias}
                      error={
                        flagBlank && !row.alias.trim()
                          ? requiredError(t('onboarding:locationOrg.locationType.aliasLabel'))
                          : undefined
                      }
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
        <div className="nhsn-link__field-group nhsn-link__location-org-tab-top">
          <FieldLabel checked={false} tooltip={t('onboarding:locationOrg.managingOrg.tooltip')}>{t('onboarding:locationOrg.managingOrg.listLabel')}</FieldLabel>
          <RepeatableList<string>
            items={managingOrganizations}
            onChange={rows => {
              trackRemoval(managingOrganizations, rows);
              patch('locationOrg', {managingOrganizationIds: rows});
            }}
            newItem={() => ''}
            minItems={0}
            addLabel={t('onboarding:locationOrg.managingOrg.add')}
            removeLabel={t('common:actions.remove')}
            renderItem={(row, index, onRowChange) => (
              <TextField
                id={`managing-org-${index}`}
                label={t('onboarding:locationOrg.managingOrg.listLabel')}
                value={row}
                error={
                  isRowFlagged(index) && !row.trim()
                    ? requiredError(t('onboarding:locationOrg.managingOrg.placeholder'))
                    : undefined
                }
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
              onDownload={() => api.getLocationOrgResolutionPdf()}
              fileName="Location_Org_Resolution.pdf"
              description={t('onboarding:locationOrg.locationIdentifier.instructions')}
              linkText={t('onboarding:locationOrg.locationIdentifier.downloadPdf')}
            />
          )}

          <div className="nhsn-link__field-group">
            <RepeatableList<LocationIdentifierEntry>
              items={locationIdentifiers}
              onChange={rows => {
                trackRemoval(locationIdentifiers, rows);
                patch('locationOrg', {locationIdentifiers: rows});
              }}
              newItem={() => ({system: '', code: ''})}
              minItems={0}
              addLabel={t('onboarding:locationOrg.locationIdentifier.add')}
              removeLabel={t('common:actions.remove')}
              columnHeadings={[
                t('onboarding:locationOrg.locationIdentifier.systemPlaceholder'),
                t('onboarding:locationOrg.locationIdentifier.codePlaceholder')
              ]}
              renderItem={(row, index, onRowChange) => {
                const flagBlank = isRowFlagged(index);
                return (
                  <>
                    <TextField
                      id={`location-identifier-system-${index}`}
                      label={t('onboarding:locationOrg.locationIdentifier.systemLabel')}
                      value={row.system}
                      error={
                        flagBlank && !row.system.trim()
                          ? requiredError(t('onboarding:locationOrg.locationIdentifier.systemLabel'))
                          : undefined
                      }
                      onChange={system => onRowChange({...row, system})}
                    />
                    <TextField
                      id={`location-identifier-code-${index}`}
                      label={t('onboarding:locationOrg.locationIdentifier.codeLabel')}
                      value={row.code}
                      error={
                        flagBlank && !row.code.trim()
                          ? requiredError(t('onboarding:locationOrg.locationIdentifier.codeLabel'))
                          : undefined
                      }
                      onChange={code => onRowChange({...row, code})}
                    />
                  </>
                );
              }}
            />
          </div>
        </>
      )}

            {activeMethod === 'custom-fhir-path' && (
              <div className="nhsn-link__location-org-tab-top">{customFhirPathContent}</div>
            )}
          </Tabs>
        </div>
      )}

      {methods.length === 0 && customFhirPathContent}

      <Modal
        open={searchOpen}
        title={acronymTitle(<HeadingPause>{t('onboarding:locationOrg.locationType.searchTitle', {vendor: vendorName})}</HeadingPause>)}
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
            candidates={availableCandidates}
            selected={selectedCandidates}
            onToggle={(id, checked) => setSelectedCandidates(current => ({...current, [id]: checked}))}
            onToggleAll={checked =>
              setSelectedCandidates(Object.fromEntries(availableCandidates.map(candidate => [candidate.id, checked])))
            }
            selectAllLabel={t('onboarding:locationOrg.locationType.selectAll')}
            columnLabels={{
              id: t('onboarding:locationOrg.locationType.columns.id'),
              alias: t('onboarding:locationOrg.locationType.columns.alias'),
              type: t('onboarding:locationOrg.locationType.columns.type'),
              codings: t('onboarding:locationOrg.locationType.columns.codings')
            }}
            emptyLabel={
              candidates.length > 0
                ? t('onboarding:locationOrg.locationType.allCandidatesAdded')
                : t('onboarding:locationOrg.locationType.noCandidates')
            }
          />
        )}
      </Modal>

      {continueAttempted && hasFlaggedIncompleteRows && (
        <div aria-live="off">
          <p className="nhsn-link__form-error" role="alert">
            {t('onboarding:locationOrg.errors.incompleteRows')}
          </p>
        </div>
      )}
    </div>
  );
}

export default LocationOrgStep;

function candidateTypeCode(candidate: LocationCandidate): string {
  return candidate.typeCodings?.[0]?.code ?? '';
}

function locationTypeKey(code: string, alias: string): string {
  return `${code.trim().toLowerCase()}|${alias.trim().toLowerCase()}`;
}

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
  onToggleAll: (checked: boolean) => void;
  selectAllLabel: string;
  columnLabels: {id: string; alias: string; type: string; codings: string};
  emptyLabel: string;
  caption: string;
}

/** The draft field each method's tab edits. */
const METHOD_FIELDS: Record<LocationMethod, keyof Omit<LocationOrgDraft, 'method'>> = {
  'managing-org': 'managingOrganizationIds',
  'location-identifier': 'locationIdentifiers',
  'location-type': 'locationTypes',
  'custom-fhir-path': 'customFhirPath'
};

/** Multi-select over search results. Columns match the POC's Cerner site-location table. */
function CandidateTable({
  candidates,
  selected,
  onToggle,
  onToggleAll,
  selectAllLabel,
  columnLabels,
  emptyLabel,
  caption
}: CandidateTableProps) {
  const selectedCount = candidates.filter(candidate => selected[candidate.id]).length;
  const allSelected = candidates.length > 0 && selectedCount === candidates.length;
  const someSelected = selectedCount > 0 && !allSelected;
  const selectAllRef = useRef<HTMLInputElement>(null);

  // `indeterminate` has no HTML attribute - it can only be set on the element itself.
  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = someSelected;
    }
  }, [someSelected]);

  if (candidates.length === 0) {
    return <p className="nhsn-link__hint-text">{emptyLabel}</p>;
  }

  return (
    <div className="nhsn-link__table-scroll" tabIndex={-1}>
      <table className="nhsn-link__table">
        <caption className="nhsn-link__visually-hidden">{caption}</caption>
        <thead>
          <tr>
            <th scope="col" className="nhsn-link__table-select-column">
              <input
                ref={selectAllRef}
                type="checkbox"
                checked={allSelected}
                aria-label={selectAllLabel}
                onChange={event => onToggleAll(event.target.checked)}
              />
            </th>
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
