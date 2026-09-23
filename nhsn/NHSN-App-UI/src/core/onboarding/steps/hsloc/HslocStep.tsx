import React, {useEffect, useMemo, useRef, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {HslocCode, HslocFacilityType, HslocMapping} from '../../../api/contracts';
import {
  AcronymText,
  acronymLabel,
  acronymTitle,
  Button,
  FieldLabel,
  HeadingPause,
  NewTabAnnouncement,
  NHSNLoadingIndicator,
  RepeatableList,
  SidePanel,
  SidePanelLayout,
  StepActions,
  TableCaption,
  Tabs,
  TextField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding, useStepValidator} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import {findDuplicateSourceCodeIndexes, findIncompleteRowIndexes, isRowBlank, isRowComplete} from './validate';
import './HslocStep.css';

type HslocTab = 'mapping' | 'reference';

/** Which fields show their "required" error. Set only by a Continue attempt, and cleared again the
 *  moment that field is edited - so emptying a field while editing never flags it until the facility
 *  next tries to move on. */
interface RowDirtyState {
  sourceDisplay: boolean;
  sourceCode: boolean;
  hslocCode: boolean;
}

const CLEAN_ROW: RowDirtyState = {sourceDisplay: false, sourceCode: false, hslocCode: false};
const ALL_DIRTY: RowDirtyState = {sourceDisplay: true, sourceCode: true, hslocCode: true};

interface MappingRow {
  sourceDisplay: string;
  sourceCode: string;
  hslocCode: string;
  dirty: RowDirtyState;
}

function toMappingRow(mapping: HslocMapping): MappingRow {
  return {
    sourceDisplay: mapping.sourceDisplay ?? '',
    sourceCode: mapping.sourceCode,
    hslocCode: mapping.hslocCode,
    dirty: CLEAN_ROW
  };
}

function hasFlaggedField(row: MappingRow): boolean {
  return row.dirty.sourceDisplay || row.dirty.sourceCode || row.dirty.hslocCode;
}

function blankMappingRow(): MappingRow {
  return {sourceDisplay: '', sourceCode: '', hslocCode: '', dirty: CLEAN_ROW};
}

function toMappings(rows: MappingRow[]): HslocMapping[] {
  return rows.map(row => ({
    sourceCode: row.sourceCode.trim(),
    sourceDisplay: row.sourceDisplay.trim() || undefined,
    hslocCode: row.hslocCode.trim()
  }));
}

/** Badge render order — matches the POC's HSLOC_FACILITY_TYPES list. */
const FACILITY_TYPE_ORDER: HslocFacilityType[] = [
  'acuteCareAll',
  'ltac',
  'ltc',
  'inpatientRehab',
  'outpatientSurgery',
  'outpatientDialysis',
  'oncology',
  'inpatientPsych'
];

/**
 * Location Identification (HSLOC). UI follows Organization Identification's
 * conventions (Tabs, FieldLabel + RepeatableList/TextField, SidePanel, the
 * shared `nhsn-link__table`) rather than step-specific markup — see
 * HslocStep.css and the "shared step blocks" section of NHSNLink.css.
 */
export function HslocStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError} = useNotifications();
  const {draft, patch, saving, vendorProfile} = useOnboarding();

  const [submitting, setSubmitting] = useState(false);
  const [tab, setTab] = useState<HslocTab>('mapping');
  const [mappingsLoading, setMappingsLoading] = useState(true);
  const [rows, setRows] = useState<MappingRow[]>(() => [blankMappingRow()]);
  const [readyToAdvance, setReadyToAdvance] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);
  const hasSyncedInitialLoad = useRef(false);

  function announceValidationMessage(message: string) {
    setValidationError(null);
    window.setTimeout(() => setValidationError(message), 0);
  }

  // A manual-upload row whose HSLOC Reference Code didn't resolve against the reference table comes
  // back from the import with a blank hslocCode, specifically so the facility can still see the
  // location and pick a code for it here - see ManualUploadTemplateService.BuildHslocAsync. It can
  // never appear in api.getHslocMappings() below, though: Normalization's hsloc-mappings row is
  // foreign-keyed to a resolved HSLOC id, so a mapping with no code was never actually persisted.
  // Captured once, at mount - not read again below - so a later edit to this same source code (which
  // does persist) isn't shadowed by the stale pending copy on a second render.
  const pendingImportRowsRef = useRef(
    (draft.hsloc.mappings ?? [])
      .filter(mapping => !mapping.hslocCode.trim() && mapping.sourceCode.trim())
      .map(toMappingRow)
  );

  const {
    data: codes = [],
    isLoading: codesLoading,
    error: codesError
  } = useQuery({
    queryKey: ['hslocCodes'],
    queryFn: () => api.getHslocCodes(),
    staleTime: Infinity
  });

  const [search, setSearch] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const [selectedCode, setSelectedCode] = useState<string | null>(null);

  // Mirrors FhirStep: patch() dispatches, and goNext must not run until that
  // dispatch has landed, or goTo() persists the draft from a stale closure.
  useEffect(() => {
    if (readyToAdvance) {
      setReadyToAdvance(false);
      onNext();
    }
  }, [readyToAdvance, onNext]);

  useEffect(() => {
    let mounted = true;
    setMappingsLoading(true);

    api
      .getHslocMappings()
      .then(mappings => {
        if (!mounted) {
          return;
        }
        const persisted = mappings.map(toMappingRow);
        const persistedSourceCodes = new Set(persisted.map(row => row.sourceCode.trim().toLowerCase()));
        const pending = pendingImportRowsRef.current.filter(
          row => !persistedSourceCodes.has(row.sourceCode.trim().toLowerCase())
        );
        const loaded = [...persisted, ...pending];
        // Always at least one row to type into - the list's minItems keeps it from being removed.
        setRows(loaded.length > 0 ? loaded : [blankMappingRow()]);
      })
      .catch(cause => {
        notifyError(cause instanceof Error ? cause.message : t('onboarding:hsloc.messages.loadError'));
      })
      .finally(() => {
        if (mounted) {
          setMappingsLoading(false);
        }
      });

    return () => {
      mounted = false;
    };
  }, [api]);

  // Marks the draft dirty as soon as the user edits a row. Skips the render where the
  // async load above first settles `rows` -- that's the mappings arriving, not an edit.
  useEffect(() => {
    if (mappingsLoading) {
      return;
    }
    if (!hasSyncedInitialLoad.current) {
      hasSyncedInitialLoad.current = true;
      return;
    }
    patch('hsloc', {mappings: toMappings(rows)});
  }, [rows, mappingsLoading, patch]);

  useEffect(() => {
    if (codesError) {
      notifyError(codesError instanceof Error ? codesError.message : t('onboarding:hsloc.messages.loadError'));
    }
  }, [codesError]);

  const loading = mappingsLoading || codesLoading;

  const locationValueLabel = vendorProfile?.hslocSourceLabel ?? t('onboarding:hsloc.mapping.fields.locationValueFallback');
  const yourCodeLabel = t('onboarding:hsloc.mapping.fields.yourCodePlaceholder');
  const hslocCodeLabel = t('onboarding:hsloc.mapping.fields.hslocCodePlaceholder');
  const hslocCodeHeading = t('onboarding:hsloc.mapping.fields.hslocCodeHeading');
  const mappingColumnHeadings = [yourCodeLabel, locationValueLabel, hslocCodeHeading];

  function isCodeMapped(code: string): boolean {
    return rows.some(row => row.hslocCode === code && row.sourceCode.trim());
  }

  const completeRows = useMemo(() => rows.filter(isRowComplete), [rows]);
  const duplicateRowIndexes = useMemo(() => new Set(findDuplicateSourceCodeIndexes(rows)), [rows]);
  const yourCodeRequiredError = t('onboarding:hsloc.mapping.fields.fieldRequiredError', {field: yourCodeLabel});
  const locationValueRequiredError = t('onboarding:hsloc.mapping.fields.fieldRequiredError', {field: locationValueLabel});
  const hslocCodeRequiredError = t('onboarding:hsloc.mapping.fields.hslocCodeRequiredError');
  const duplicateFieldError = t('onboarding:hsloc.mapping.fields.duplicateError');

  const categories = useMemo(
    () => Array.from(new Set(codes.map(row => row.category).filter((value): value is string => Boolean(value)))).sort(),
    [codes]
  );
  const types = useMemo(
    () => Array.from(new Set(codes.map(row => row.type).filter((value): value is string => Boolean(value)))).sort(),
    [codes]
  );

  const filteredCodes = useMemo(() => {
    const query = search.trim().toLowerCase();
    return codes.filter(row => {
      if (categoryFilter && row.category !== categoryFilter) {
        return false;
      }
      if (typeFilter && row.type !== typeFilter) {
        return false;
      }
      if (!query) {
        return true;
      }
      const haystack = [row.category, row.type, row.code, row.display, row.definition]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return haystack.includes(query);
    });
  }, [codes, search, categoryFilter, typeFilter]);

  // Keeps the detail panel's selection inside whatever the current filters allow.
  useEffect(() => {
    if (filteredCodes.length === 0) {
      setSelectedCode(null);
      return;
    }
    if (!filteredCodes.some(row => row.code === selectedCode)) {
      setSelectedCode(filteredCodes[0].code);
    }
  }, [filteredCodes, selectedCode]);

  const selectedRow = filteredCodes.find(row => row.code === selectedCode) ?? null;
  const mappedRowsForSelected = selectedRow
    ? rows.filter(row => row.hslocCode === selectedRow.code && row.sourceCode.trim())
    : [];

  // <optgroup> options for the mapping row's HSLOC select, grouped by category.
  const groupedCodeOptions = useMemo(() => {
    const byCategory = new Map<string, HslocCode[]>();
    codes.forEach(row => {
      const key = row.category ?? '';
      const group = byCategory.get(key);
      if (group) {
        group.push(row);
      } else {
        byCategory.set(key, [row]);
      }
    });
    return Array.from(byCategory.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [codes]);

  /** The step-level message `rows` currently deserves, or null when they're fine to save. */
  function validationMessageFor(current: MappingRow[]): string | null {
    if (current.every(isRowBlank)) {
      return t('onboarding:hsloc.messages.empty');
    }
    if (findIncompleteRowIndexes(current).length > 0) {
      return t('onboarding:hsloc.messages.incomplete');
    }
    if (findDuplicateSourceCodeIndexes(current).length > 0) {
      return t('onboarding:hsloc.messages.duplicate');
    }
    return null;
  }

  // Once shown, the message follows the rows live - fixing the problem clears it (or swaps it for
  // the next one) without another click on Continue. A row added since that Continue isn't flagged
  // yet (its fields show no errors), so it doesn't hold the message up either until the next attempt.
  useEffect(() => {
    if (validationError === null) {
      return;
    }
    const tracked = rows.filter(row => hasFlaggedField(row) || isRowComplete(row));
    const message =
      tracked.length > 0
        ? validationMessageFor(tracked)
        : rows.every(isRowBlank)
          ? t('onboarding:hsloc.messages.empty')
          : null;
    if (message !== validationError) {
      setValidationError(message);
    }
  }, [rows]);

  function validateStep(): boolean {
    const message = validationMessageFor(rows);
    if (message === null) {
      setValidationError(null);
      return true;
    }
    const incompleteRowIndexes = rows.every(isRowBlank)
      ? rows.map((_row, index) => index)
      : findIncompleteRowIndexes(rows);
    if (incompleteRowIndexes.length > 0) {
      setRows(prev =>
        prev.map((row, index) => (incompleteRowIndexes.includes(index) ? {...row, dirty: ALL_DIRTY} : row))
      );
    }
    announceValidationMessage(message);
    return false;
  }

  async function handleNext() {
    if (!validateStep()) {
      return;
    }

    setSubmitting(true);
    try {
      const mappings = toMappings(completeRows);

      await api.saveHslocMappings(mappings);
      patch('hsloc', {mappings});
      setReadyToAdvance(true);
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:hsloc.messages.saveError'));
    } finally {
      setSubmitting(false);
    }
  }

  useStepValidator(validateStep);

  const busy = saving || submitting;
  const stableOnBack = useStableCallback(onBack);
  const stableHandleNext = useStableCallback(handleNext);

  useStepChrome(
    useMemo(
      () =>
        loading
          ? null
          : {
              title: acronymTitle(<HeadingPause><AcronymText>{t('onboarding:hsloc.title')}</AcronymText></HeadingPause>),
              footer: (
                <StepActions saving={busy}>
                  <Button variant="secondary" onClick={stableOnBack} disabled={busy}>
                    {t('common:actions.back')}
                  </Button>
                  <Button onClick={stableHandleNext} disabled={busy}>
                    {t('common:actions.continue')}
                  </Button>
                </StepActions>
              )
            },
      [t, loading, busy, stableOnBack, stableHandleNext]
    )
  );

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="nhsn-link__hsloc">
      <p className="nhsn-link__subtitle">
        <AcronymText>{t('onboarding:hsloc.subtitlePrefix')}</AcronymText>{' '}
        <a
          href="https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html"
          target="_blank"
          rel="noreferrer">
          {t('onboarding:hsloc.subtitleLinkText')}
          <NewTabAnnouncement />
        </a>
        <AcronymText>{t('onboarding:hsloc.subtitleSuffix')}</AcronymText>
      </p>

      <div className="nhsn-link__field-group">
        <Tabs<HslocTab>
          label={acronymLabel(t('onboarding:hsloc.title'))}
          tabs={[
            {id: 'mapping', label: t('onboarding:hsloc.tabs.mapping')},
            {id: 'reference', label: <AcronymText>{t('onboarding:hsloc.tabs.reference')}</AcronymText>}
          ]}
          activeTab={tab}
          onTabChange={setTab}>
          {tab === 'mapping' && (
        <div className="nhsn-link__field-group nhsn-link__hsloc-mapping">
          <RepeatableList<MappingRow>
            items={rows}
            onChange={setRows}
            newItem={blankMappingRow}
            minItems={1}
            addLabel={t('onboarding:hsloc.mapping.addButton')}
            removeLabel={t('common:actions.remove')}
            columnHeadings={mappingColumnHeadings}
            renderItem={(row, index, onRowChange) => {
              const sourceDisplayInvalid = row.dirty.sourceDisplay && !row.sourceDisplay.trim();
              const sourceCodeInvalid = row.dirty.sourceCode && !row.sourceCode.trim();
              // A repeat is wrong the moment it's typed, so unlike a blank field it doesn't wait for Continue.
              const sourceCodeDuplicate = !sourceCodeInvalid && duplicateRowIndexes.has(index);
              const hslocCodeInvalid = row.dirty.hslocCode && !row.hslocCode.trim();
              return (
                <>
                  <TextField
                    id={`hsloc-your-code-${index}`}
                    label={yourCodeLabel}
                    required
                    value={row.sourceDisplay}
                    error={sourceDisplayInvalid ? yourCodeRequiredError : undefined}
                    onChange={sourceDisplay =>
                      onRowChange({...row, sourceDisplay, dirty: {...row.dirty, sourceDisplay: false}})
                    }
                  />
                  <TextField
                    id={`hsloc-location-value-${index}`}
                    label={locationValueLabel}
                    required
                    value={row.sourceCode}
                    error={
                      sourceCodeInvalid ? locationValueRequiredError : sourceCodeDuplicate ? duplicateFieldError : undefined
                    }
                    onChange={sourceCode => onRowChange({...row, sourceCode, dirty: {...row.dirty, sourceCode: false}})}
                  />
                  <div>
                    <select
                      id={`hsloc-code-select-${index}`}
                      className={
                        hslocCodeInvalid
                          ? 'nhsn-link__hsloc-code-select nhsn-link__hsloc-code-select--error'
                          : 'nhsn-link__hsloc-code-select'
                      }
                      aria-label={acronymLabel(hslocCodeLabel)}
                      aria-invalid={hslocCodeInvalid}
                      aria-required="true"
                      aria-describedby={hslocCodeInvalid ? `hsloc-code-error-${index}` : undefined}
                      required
                      value={row.hslocCode}
                      onChange={event =>
                        onRowChange({...row, hslocCode: event.target.value, dirty: {...row.dirty, hslocCode: false}})
                      }>
                      <option value="">{hslocCodeLabel}</option>
                      {groupedCodeOptions.map(([category, categoryCodes]) => (
                        <optgroup label={category || t('onboarding:hsloc.reference.allCategories')} key={category}>
                          {categoryCodes.map(code => (
                            <option value={code.code} key={code.code}>
                              {code.code} - {code.display}
                            </option>
                          ))}
                        </optgroup>
                      ))}
                    </select>
                    {hslocCodeInvalid && (
                      <p id={`hsloc-code-error-${index}`} className="nhsn-link__hsloc-code-error-text" role="alert">
                        {hslocCodeRequiredError}
                      </p>
                    )}
                  </div>
                </>
              );
            }}
          />
        </div>
      )}

      {tab === 'reference' && (
        <SidePanelLayout>
          <div>
            <div className="nhsn-link__field-group">
              <input
                type="text"
                className="nhsn-link__hsloc-search"
                placeholder={t('onboarding:hsloc.reference.searchPlaceholder')}
                aria-label={t('onboarding:hsloc.reference.searchPlaceholder')}
                value={search}
                onChange={event => setSearch(event.target.value)}
              />
              <div className="nhsn-link__hsloc-filter-row">
                <select
                  aria-label={t('onboarding:hsloc.reference.allCategories')}
                  value={categoryFilter}
                  onChange={event => setCategoryFilter(event.target.value)}>
                  <option value="">{t('onboarding:hsloc.reference.allCategories')}</option>
                  {categories.map(category => (
                    <option value={category} key={category}>
                      {category}
                    </option>
                  ))}
                </select>
                <select
                  aria-label={t('onboarding:hsloc.reference.allTypes')}
                  value={typeFilter}
                  onChange={event => setTypeFilter(event.target.value)}>
                  <option value="">{t('onboarding:hsloc.reference.allTypes')}</option>
                  {types.map(type => (
                    <option value={type} key={type}>
                      {type}
                    </option>
                  ))}
                </select>
              </div>
              <p className="nhsn-link__hint-text">
                <AcronymText>{t('onboarding:hsloc.reference.resultCount', {count: filteredCodes.length, total: codes.length})}</AcronymText>
              </p>
            </div>

            <div className="nhsn-link__table-scroll nhsn-link__hsloc-table-scroll" tabIndex={-1}>
              <table className="nhsn-link__table nhsn-link__hsloc-table">
                <TableCaption><AcronymText>{t('onboarding:hsloc.tabs.reference')}</AcronymText></TableCaption>
                <thead>
                  <tr>
                    <th scope="col">{t('onboarding:hsloc.reference.columns.category')}</th>
                    <th scope="col">{t('onboarding:hsloc.reference.columns.type')}</th>
                    <th scope="col">{t('onboarding:hsloc.reference.columns.code')}</th>
                    <th scope="col">{t('onboarding:hsloc.reference.columns.description')}</th>
                    <th scope="col">{t('onboarding:hsloc.reference.columns.facilities')}</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredCodes.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="nhsn-link__hsloc-empty-row">
                        <AcronymText>{t('onboarding:hsloc.reference.noResults')}</AcronymText>
                      </td>
                    </tr>
                  ) : (
                    filteredCodes.map(row => {
                      const mapped = isCodeMapped(row.code);
                      return (
                        <tr
                          key={row.code}
                          className={`nhsn-link__hsloc-row${row.code === selectedCode ? ' nhsn-link__hsloc-row--selected' : ''}`}
                          onClick={() => setSelectedCode(row.code)}>
                          <td className={mapped ? 'nhsn-link__hsloc-mapped-cell' : undefined}>{row.category}</td>
                          <td className={mapped ? 'nhsn-link__hsloc-mapped-cell' : undefined}>{row.type}</td>
                          <td className={mapped ? 'nhsn-link__hsloc-mapped-cell' : undefined}>{row.code}</td>
                          <td className={mapped ? 'nhsn-link__hsloc-mapped-cell' : undefined}>{row.display}</td>
                          <td>
                            <div className="hsloc-badges">
                              {FACILITY_TYPE_ORDER.filter(type => row.facilityTypes?.includes(type)).map(type => (
                                <span
                                  key={type}
                                  className={`hsloc-badge hsloc-badge--${type}`}
                                  title={t(`onboarding:hsloc.reference.facilityTypeTitles.${type}`)}>
                                  {t(`onboarding:hsloc.reference.facilityTypes.${type}`)}
                                </span>
                              ))}
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <SidePanel>
            {selectedRow ? (
              <>
                <div className="nhsn-link__hsloc-detail-code">{selectedRow.code}</div>
                <p className="nhsn-link__hsloc-detail-description">{selectedRow.display}</p>
                {selectedRow.definition && <p className="nhsn-link__hint-text">{selectedRow.definition}</p>}
                <FieldLabel checked={false}>{t('onboarding:hsloc.reference.detail.mappedHeading')}</FieldLabel>
                {mappedRowsForSelected.length === 0 ? (
                  <p className="nhsn-link__hint-text"><AcronymText>{t('onboarding:hsloc.reference.detail.noneMapped')}</AcronymText></p>
                ) : (
                  <ul className="nhsn-link__summary-list">
                    {mappedRowsForSelected.map((row, index) => (
                      <li key={index}>
                        <span>{row.sourceCode}</span>
                        <span>{row.sourceDisplay || t('onboarding:hsloc.reference.detail.noCode')}</span>
                      </li>
                    ))}
                  </ul>
                )}
              </>
            ) : (
              <p className="nhsn-link__hint-text">{t('onboarding:hsloc.reference.detail.selectPrompt')}</p>
            )}
          </SidePanel>
        </SidePanelLayout>
          )}
        </Tabs>
      </div>

      <div aria-live="off">
        <p className="nhsn-link__form-error" role="alert">
          {validationError && <AcronymText>{validationError}</AcronymText>}
        </p>
      </div>
    </div>
  );
}

export default HslocStep;
