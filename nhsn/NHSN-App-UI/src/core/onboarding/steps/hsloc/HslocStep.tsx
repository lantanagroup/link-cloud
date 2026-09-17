import React, {useEffect, useMemo, useRef, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {HslocCode, HslocFacilityType, HslocMapping} from '../../../api/contracts';
import {
  Button,
  FieldLabel,
  NewTabAnnouncement,
  NHSNLoadingIndicator,
  PageHeader,
  RepeatableList,
  SidePanel,
  SidePanelLayout,
  StepActions,
  Tabs,
  TextField
} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {findDuplicateSourceCodeIndexes, findIncompleteRowIndexes} from './validate';
import './HslocStep.css';

type HslocTab = 'mapping' | 'reference';

interface RowDirtyState {
  sourceDisplay: boolean;
  sourceCode: boolean;
  hslocCode: boolean;
}

const CLEAN_ROW: RowDirtyState = {sourceDisplay: false, sourceCode: false, hslocCode: false};

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
  const {patch, saving, vendorProfile} = useOnboarding();

  const [submitting, setSubmitting] = useState(false);
  const [tab, setTab] = useState<HslocTab>('mapping');
  const [mappingsLoading, setMappingsLoading] = useState(true);
  const [rows, setRows] = useState<MappingRow[]>([]);
  const [readyToAdvance, setReadyToAdvance] = useState(false);
  const hasSyncedInitialLoad = useRef(false);

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
        setRows(mappings.map(toMappingRow));
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

  function isCodeMapped(code: string): boolean {
    return rows.some(row => row.hslocCode === code && row.sourceCode.trim());
  }

  const incompleteRowIndexes = useMemo(() => new Set(findIncompleteRowIndexes(rows)), [rows]);
  const duplicateRowIndexes = useMemo(() => new Set(findDuplicateSourceCodeIndexes(rows)), [rows]);
  const requiredFieldError = t('onboarding:hsloc.mapping.fields.requiredError');
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

  async function handleNext() {
    if (incompleteRowIndexes.size > 0) {
      notifyError(t('onboarding:hsloc.messages.incomplete'));
      return;
    }
    if (duplicateRowIndexes.size > 0) {
      notifyError(t('onboarding:hsloc.messages.duplicate'));
      return;
    }

    setSubmitting(true);
    try {
      const mappings = toMappings(rows);

      await api.saveHslocMappings(mappings);
      patch('hsloc', {mappings});
      setReadyToAdvance(true);
    } catch (cause) {
      notifyError(cause instanceof Error ? cause.message : t('onboarding:hsloc.messages.saveError'));
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  const busy = saving || submitting;

  return (
    <div className="nhsn-link__content nhsn-link__hsloc">
      <PageHeader title={t('onboarding:hsloc.title')} />
      <p className="nhsn-link__subtitle">
        {t('onboarding:hsloc.subtitlePrefix')}{' '}
        <a
          href="https://www.cdc.gov/nhsn/cdaportal/terminology/codesystem/hsloc.html"
          target="_blank"
          rel="noreferrer">
          {t('onboarding:hsloc.subtitleLinkText')}
          <NewTabAnnouncement />
        </a>
        {t('onboarding:hsloc.subtitleSuffix')}
      </p>

      <div className="nhsn-link__field-group">
        <Tabs<HslocTab>
          label={t('onboarding:hsloc.title')}
          tabs={[
            {id: 'mapping', label: t('onboarding:hsloc.tabs.mapping')},
            {id: 'reference', label: t('onboarding:hsloc.tabs.reference')}
          ]}
          activeTab={tab}
          onTabChange={setTab}
        />
      </div>

      {tab === 'mapping' && (
        <div className="nhsn-link__field-group">
          <FieldLabel checked={false}>{t('onboarding:hsloc.mapping.listLabel')}</FieldLabel>
          <RepeatableList<MappingRow>
            items={rows}
            onChange={setRows}
            newItem={() => ({sourceDisplay: '', sourceCode: '', hslocCode: '', dirty: CLEAN_ROW})}
            addLabel={t('onboarding:hsloc.mapping.addButton')}
            removeLabel={t('common:actions.remove')}
            emptyLabel={t('onboarding:hsloc.mapping.emptyState')}
            renderItem={(row, index, onRowChange) => {
              const sourceDisplayInvalid = row.dirty.sourceDisplay && !row.sourceDisplay.trim();
              const sourceCodeInvalid = row.dirty.sourceCode && !row.sourceCode.trim();
              const sourceCodeDuplicate = row.dirty.sourceCode && !sourceCodeInvalid && duplicateRowIndexes.has(index);
              const hslocCodeInvalid = row.dirty.hslocCode && !row.hslocCode.trim();
              return (
                <>
                  <TextField
                    id={`hsloc-your-code-${index}`}
                    label={yourCodeLabel}
                    placeholder={yourCodeLabel}
                    value={row.sourceDisplay}
                    error={sourceDisplayInvalid ? requiredFieldError : undefined}
                    onChange={sourceDisplay =>
                      onRowChange({...row, sourceDisplay, dirty: {...row.dirty, sourceDisplay: true}})
                    }
                  />
                  <TextField
                    id={`hsloc-location-value-${index}`}
                    label={locationValueLabel}
                    placeholder={locationValueLabel}
                    value={row.sourceCode}
                    error={sourceCodeInvalid ? requiredFieldError : sourceCodeDuplicate ? duplicateFieldError : undefined}
                    onChange={sourceCode => onRowChange({...row, sourceCode, dirty: {...row.dirty, sourceCode: true}})}
                  />
                  <div>
                    <select
                      className={
                        hslocCodeInvalid
                          ? 'nhsn-link__hsloc-code-select nhsn-link__hsloc-code-select--error'
                          : 'nhsn-link__hsloc-code-select'
                      }
                      aria-label={hslocCodeLabel}
                      aria-invalid={hslocCodeInvalid}
                      value={row.hslocCode}
                      onChange={event =>
                        onRowChange({...row, hslocCode: event.target.value, dirty: {...row.dirty, hslocCode: true}})
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
                    {hslocCodeInvalid && <p className="nhsn-link__hsloc-code-error-text">{requiredFieldError}</p>}
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
                {t('onboarding:hsloc.reference.resultCount', {count: filteredCodes.length, total: codes.length})}
              </p>
            </div>

            <div className="nhsn-link__table-scroll nhsn-link__hsloc-table-scroll">
              <table className="nhsn-link__table nhsn-link__hsloc-table">
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
                        {t('onboarding:hsloc.reference.noResults')}
                      </td>
                    </tr>
                  ) : (
                    filteredCodes.map(row => {
                      const mapped = isCodeMapped(row.code);
                      return (
                        <tr
                          key={row.code}
                          className={`nhsn-link__hsloc-row${row.code === selectedCode ? ' nhsn-link__hsloc-row--selected' : ''}`}
                          tabIndex={0}
                          aria-selected={row.code === selectedCode}
                          onClick={() => setSelectedCode(row.code)}
                          onKeyDown={event => {
                            if (event.key === 'Enter' || event.key === ' ') {
                              event.preventDefault();
                              setSelectedCode(row.code);
                            }
                          }}>
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
                  <p className="nhsn-link__hint-text">{t('onboarding:hsloc.reference.detail.noneMapped')}</p>
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

      <StepActions saving={busy}>
        <Button variant="secondary" onClick={onBack} disabled={busy}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={handleNext} disabled={busy}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default HslocStep;
