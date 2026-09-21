import React, {useEffect, useMemo, useRef, useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {Trans, useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {EncounterCode, EncounterCodeDetail, EncounterMapping} from '../../../api/contracts';
import {Button, NewTabAnnouncement, NHSNLoadingIndicator, Select, StepActions, Tabs, TextField} from '../../../fields';
import {useNotifications} from '../../../notifications/NotificationProvider';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';
import {findIncompleteRowKeys} from './validate';
import './EncounterStep.css';

/**
 * Step scaffold. The screen's own fields, validation and API calls are LEGLINK story: encounter mapping.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.
 */

/** Exported for the Report Details "Encounter Mapping" modal, which reads the same shape read-only. */
export interface MappingRowState {
  rowKey: string;
  localValue: string;
  targetSystem: string;
  targetCode: string;
  targetDisplay: string;
}

export interface CodeSystemGroupState {
  groupKey: string;
  codeSystem: string;
  mappings: MappingRowState[];
}

export function EncounterStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const {notifyError} = useNotifications();
  const {draft, patch, saving} = useOnboarding();

  const [groups, setGroups] = useState<CodeSystemGroupState[]>(() =>
    buildGroups(draft.encounter.codeSystems ?? [], draft.encounter.mappings ?? [])
  );
  const {
    data: referenceCodes = [],
    isLoading: loading,
    error: referenceCodesError
  } = useQuery({
    queryKey: ['encounterCodes'],
    queryFn: () => api.getEncounterCodes(),
    staleTime: Infinity
  });
  const [activeTab, setActiveTab] = useState<'mapping' | 'reference'>('mapping');
  const [search, setSearch] = useState('');
  const [systemFilter, setSystemFilter] = useState('');
  const [categoryFilter, setCategoryFilter] = useState('');
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [readyToAdvance, setReadyToAdvance] = useState(false);
  // Captured once, at mount -- not a "have we run yet" flag, because StrictMode invokes
  // effects twice on mount against the same committed state, and a boolean flag would be
  // flipped by the first invocation and wrongly let the second one patch.
  const initialGroupsRef = useRef(groups);

  useEffect(() => {
    if (readyToAdvance) {
      setReadyToAdvance(false);
      onNext();
    }
  }, [readyToAdvance, onNext]);

  // Marks the draft dirty as soon as the user edits a code system or mapping row. `groups`
  // only differs from the mount-time reference once a setGroups mutator has actually run.
  useEffect(() => {
    if (groups === initialGroupsRef.current) {
      return;
    }
    const {codeSystems, mappings} = flattenGroups(groups);
    patch('encounter', {codeSystems, mappings});
  }, [groups, patch]);

  useEffect(() => {
    if (referenceCodesError) {
      notifyError(
        referenceCodesError instanceof Error ? referenceCodesError.message : t('onboarding:encounter.messages.loadError')
      );
    }
  }, [referenceCodesError]);

  const systemOptions = useMemo(() => {
    const systems = Array.from(new Set(referenceCodes.map(code => code.system))).sort();
    // Filter by the raw system (a FHIR canonical url, e.g. http://www.ama-assn.org/go/cpt) but show
    // the short mnemonic the badges already use (CPT/SNOMED) — the url is correct, not readable.
    return systems.map(system => ({value: system, label: systemBadgeLabel(system)}));
  }, [referenceCodes]);

  const categoryOptions = useMemo(() => {
    const byCategory = new Map<string, string>();
    referenceCodes.forEach(code => {
      if (code.category && !byCategory.has(code.category)) {
        byCategory.set(code.category, code.categoryName ?? code.category);
      }
    });
    return Array.from(byCategory.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([value, label]) => ({value, label: `${value} — ${label}`}));
  }, [referenceCodes]);

  const filteredReferenceRows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return referenceCodes
      .filter(code => {
        if (systemFilter && code.system !== systemFilter) {
          return false;
        }
        if (categoryFilter && code.category !== categoryFilter) {
          return false;
        }
        if (!q) {
          return true;
        }
        return `${code.system} ${code.category ?? ''} ${code.categoryName ?? ''} ${code.code} ${code.display}`
          .toLowerCase()
          .includes(q);
      })
      .map(code => ({...code, key: encodeTarget(code.system, code.code)}));
  }, [referenceCodes, search, systemFilter, categoryFilter]);

  useEffect(() => {
    setSelectedKey(current => {
      if (!filteredReferenceRows.length) {
        return null;
      }
      return current && filteredReferenceRows.some(row => row.key === current) ? current : filteredReferenceRows[0].key;
    });
  }, [filteredReferenceRows]);

  const allMappingRows = useMemo(
    () => groups.flatMap(group => group.mappings.map(row => ({...row, codeSystem: group.codeSystem}))),
    [groups]
  );

  const selectedReferenceRow = filteredReferenceRows.find(row => row.key === selectedKey) ?? null;

  const [codeDetail, setCodeDetail] = useState<EncounterCodeDetail | null>(null);

  useEffect(() => {
    if (!selectedReferenceRow) {
      setCodeDetail(null);
      return;
    }
    let mounted = true;
    setCodeDetail(null);
    api
      .lookupEncounterCode(selectedReferenceRow.system, selectedReferenceRow.code)
      .then(detail => {
        if (mounted) {
          setCodeDetail(detail);
        }
      })
      .catch(() => {
        if (mounted) {
          setCodeDetail(null);
        }
      });
    return () => {
      mounted = false;
    };
  }, [api, selectedReferenceRow?.system, selectedReferenceRow?.code]);

  const matchedLocalMappings = useMemo(() => {
    if (!selectedReferenceRow) {
      return [];
    }
    return allMappingRows.filter(
      row =>
        row.localValue.trim() &&
        row.targetSystem === selectedReferenceRow.system &&
        row.targetCode === selectedReferenceRow.code
    );
  }, [allMappingRows, selectedReferenceRow]);

  const incompleteRowKeys = useMemo(() => new Set(findIncompleteRowKeys(groups)), [groups]);

  function addCodeSystem() {
    setGroups(current => [...current, {groupKey: makeKey(), codeSystem: '', mappings: []}]);
  }

  function removeCodeSystem(groupKey: string) {
    setGroups(current => current.filter(group => group.groupKey !== groupKey));
  }

  function updateCodeSystem(groupKey: string, value: string) {
    setGroups(current => current.map(group => (group.groupKey === groupKey ? {...group, codeSystem: value} : group)));
  }

  function addMappingRow(groupKey: string) {
    setGroups(current =>
      current.map(group =>
        group.groupKey === groupKey
          ? {...group, mappings: [...group.mappings, {rowKey: makeKey(), localValue: '', targetSystem: '', targetCode: '', targetDisplay: ''}]}
          : group
      )
    );
  }

  function removeMappingRow(groupKey: string, rowKey: string) {
    setGroups(current =>
      current.map(group =>
        group.groupKey === groupKey
          ? {...group, mappings: group.mappings.filter(row => row.rowKey !== rowKey)}
          : group
      )
    );
  }

  function updateMappingRow(groupKey: string, rowKey: string, rowPatch: Partial<MappingRowState>) {
    setGroups(current =>
      current.map(group =>
        group.groupKey === groupKey
          ? {...group, mappings: group.mappings.map(row => (row.rowKey === rowKey ? {...row, ...rowPatch} : row))}
          : group
      )
    );
  }

  function handleNext() {
    const {codeSystems, mappings} = flattenGroups(groups);
    patch('encounter', {codeSystems, mappings});
    setReadyToAdvance(true);
  }

  const stableOnBack = useStableCallback(onBack);
  const stableHandleNext = useStableCallback(handleNext);

  useStepChrome(
    useMemo(
      () =>
        loading
          ? null
          : {
              title: t('onboarding:encounter.title'),
              footer: (
                <StepActions saving={saving}>
                  <Button variant="secondary" onClick={stableOnBack} disabled={saving}>
                    {t('common:actions.back')}
                  </Button>
                  <Button onClick={stableHandleNext} disabled={saving} loading={saving}>
                    {t('common:actions.continue')}
                  </Button>
                </StepActions>
              )
            },
      [t, loading, saving, stableOnBack, stableHandleNext]
    )
  );

  if (loading) {
    return <NHSNLoadingIndicator />;
  }

  return (
    <div className="encounter-mapping">
        <p className="subtitle">
            <Trans
              t={t}
              i18nKey="onboarding:encounter.subtitle"
              components={{
                uscore: (
                  <a
                    href="https://hl7.org/fhir/us/core/STU6.1/index.html"
                    target="_blank"
                    rel="noreferrer"
                    aria-describedby="encounter-uscore-link-hint"
                  />
                ),
                encounters: (
                  <a
                    href="https://www.hl7.org/fhir/R4/encounter.html"
                    target="_blank"
                    rel="noreferrer"
                    aria-describedby="encounter-encounters-link-hint"
                  />
                ),
                valueset: (
                  <a
                    href="https://hl7.org/fhir/us/core/STU6.1/ValueSet-us-core-encounter-type.html"
                    target="_blank"
                    rel="noreferrer"
                    aria-describedby="encounter-valueset-link-hint"
                  />
                )
              }}
            />
            <NewTabAnnouncement id="encounter-uscore-link-hint" />
            <NewTabAnnouncement id="encounter-encounters-link-hint" />
            <NewTabAnnouncement id="encounter-valueset-link-hint" />
          </p>

          <Tabs<'mapping' | 'reference'>
            label={t('onboarding:encounter.title')}
            tabs={[
              {id: 'mapping', label: t('onboarding:encounter.tabs.mapping')},
              {id: 'reference', label: t('onboarding:encounter.tabs.reference')}
            ]}
            activeTab={activeTab}
            onTabChange={setActiveTab}
          />

          {activeTab === 'mapping' && (
            <div>
              <div className="form-group">
                <span className="section-label">
                  {t('onboarding:encounter.fields.codeSystemsLabel')}
                </span>
                <p className="form-hint">{t('onboarding:encounter.fields.codeSystemsHint')}</p>
              </div>

              {groups.length === 0 && (
                <p className="form-hint form-hint--spaced">{t('onboarding:encounter.fields.noCodeSystems')}</p>
              )}

              {groups.map(group => (
                <CodeSystemBlock
                  key={group.groupKey}
                  group={group}
                  referenceCodes={referenceCodes}
                  incompleteRowKeys={incompleteRowKeys}
                  onCodeSystemChange={value => updateCodeSystem(group.groupKey, value)}
                  onRemoveGroup={() => removeCodeSystem(group.groupKey)}
                  onAddRow={() => addMappingRow(group.groupKey)}
                  onRemoveRow={rowKey => removeMappingRow(group.groupKey, rowKey)}
                  onUpdateRow={(rowKey, rowPatch) => updateMappingRow(group.groupKey, rowKey, rowPatch)} />
              ))}

              <Button variant="secondary" size="sm" onClick={addCodeSystem} disabled={saving}>
                {t('onboarding:encounter.fields.addCodeSystem')}
              </Button>
            </div>
          )}

          {activeTab === 'reference' && (
            <div className="encounter-reference-layout">
              <div className="encounter-reference-main">
                <TextField
                  id="encounterSearch"
                  label={t('onboarding:encounter.reference.searchLabel')}
                  placeholder={t('onboarding:encounter.reference.searchPlaceholder')}
                  value={search}
                  onChange={setSearch} />

                <div className="encounter-filters">
                  <Select
                    id="encounterSystemFilter"
                    label={t('onboarding:encounter.reference.systemFilterLabel')}
                    placeholder={t('onboarding:encounter.reference.systemFilterAll')}
                    options={systemOptions}
                    value={systemFilter}
                    popupClassName="nhsn-facility-info-popup"
                    onChange={setSystemFilter} />
                  <Select
                    id="encounterCategoryFilter"
                    label={t('onboarding:encounter.reference.categoryFilterLabel')}
                    placeholder={t('onboarding:encounter.reference.categoryFilterAll')}
                    options={categoryOptions}
                    value={categoryFilter}
                    popupClassName="nhsn-facility-info-popup"
                    onChange={setCategoryFilter} />
                </div>

                <p className="form-hint">
                  {t('onboarding:encounter.reference.resultCount', {
                    count: filteredReferenceRows.length,
                    total: referenceCodes.length
                  })}
                </p>

                <div className="encounter-table-scroll" tabIndex={-1}>
                  <table className="encounter-table">
                    <caption className="nhsn-link__visually-hidden">{t('onboarding:encounter.tabs.reference')}</caption>
                    <thead>
                      <tr>
                        <th scope="col">{t('onboarding:encounter.reference.columns.system')}</th>
                        <th scope="col">{t('onboarding:encounter.reference.columns.category')}</th>
                        <th scope="col">{t('onboarding:encounter.reference.columns.code')}</th>
                        <th scope="col">{t('onboarding:encounter.reference.columns.description')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredReferenceRows.length === 0 ? (
                        <tr>
                          <td colSpan={4} className="encounter-table-empty">
                            {t('onboarding:encounter.reference.noResults')}
                          </td>
                        </tr>
                      ) : (
                        filteredReferenceRows.map(row => (
                          <tr
                            key={row.key}
                            className={row.key === selectedKey ? 'encounter-row encounter-row--selected' : 'encounter-row'}
                            onClick={() => setSelectedKey(row.key)}>
                            <td>
                              <span
                                className={`encounter-system-badge encounter-system-badge-${systemBadgeVariant(row.system)}`}
                                title={row.system}>
                                {systemBadgeLabel(row.system)}
                              </span>
                            </td>
                            <td>{row.categoryName}</td>
                            <td>{row.code}</td>
                            <td>{row.display}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              <div className="encounter-detail-panel">
                {!selectedReferenceRow ? (
                  <p className="form-hint encounter-detail-empty">{t('onboarding:encounter.reference.detailNoSelection')}</p>
                ) : (
                  <>
                    <div className="section-title">
                      <span className={`encounter-system-badge encounter-system-badge-${systemBadgeVariant(selectedReferenceRow.system)}`}>
                        {systemBadgeLabel(selectedReferenceRow.system)}
                      </span>{' '}
                      {selectedReferenceRow.code}
                    </div>
                    <p className="form-hint encounter-detail-system">{selectedReferenceRow.system}</p>
                    <p className="encounter-detail-description">{selectedReferenceRow.display}</p>
                    {selectedReferenceRow.categoryName && (
                      <p className="form-hint">
                        {selectedReferenceRow.categoryName} ({selectedReferenceRow.category})
                      </p>
                    )}
                    {codeDetail?.name && (
                      <p className="form-hint encounter-detail-codesystem">
                        {codeDetail.version
                          ? t('onboarding:encounter.reference.detailCodeSystem', {
                              name: codeDetail.name,
                              version: codeDetail.version
                            })
                          : codeDetail.name}
                      </p>
                    )}
                    <div className="section-title">{t('onboarding:encounter.reference.detailMappedTitle')}</div>
                    {matchedLocalMappings.length === 0 ? (
                      <p className="form-hint">{t('onboarding:encounter.reference.detailNoMappedCodes')}</p>
                    ) : (
                      <ul className="summary-list">
                        {matchedLocalMappings.map(row => (
                          <li key={row.rowKey}>
                            <span>{row.localValue}</span>
                            <span>{row.codeSystem}</span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </>
                )}
              </div>
            </div>
          )}
    </div>
  );
}

export default EncounterStep;

// ---------------------------------------------------------------- code system block

interface CodeSystemBlockProps {
  group: CodeSystemGroupState;
  referenceCodes: EncounterCode[];
  incompleteRowKeys: Set<string>;
  onCodeSystemChange: (value: string) => void;
  onRemoveGroup: () => void;
  onAddRow: () => void;
  onRemoveRow: (rowKey: string) => void;
  onUpdateRow: (rowKey: string, rowPatch: Partial<MappingRowState>) => void;
}

function CodeSystemBlock({
  group,
  referenceCodes,
  incompleteRowKeys,
  onCodeSystemChange,
  onRemoveGroup,
  onAddRow,
  onRemoveRow,
  onUpdateRow
}: CodeSystemBlockProps) {
  const {t} = useTranslation('onboarding');
  const codeSystemInputId = `encounter-codesystem-${group.groupKey}`;

  return (
    <div className="codesystem-block">
      <div className="form-group">
        <label htmlFor={codeSystemInputId}>{t('encounter.fields.codeSystemLabel')}</label>
        <div className="codesystem-row">
          <input
            id={codeSystemInputId}
            type="text"
            placeholder={t('encounter.fields.codeSystemPlaceholder') ?? ''}
            value={group.codeSystem}
            onChange={event => onCodeSystemChange(event.target.value)} />
          <Button
            variant="secondary"
            size="sm"
            onClick={onRemoveGroup}
            aria-label={
              group.codeSystem
                ? t('encounter.fields.removeCodeSystemAriaLabel', {system: group.codeSystem})
                : t('encounter.fields.removeCodeSystem')
            }>
            {t('encounter.fields.removeCodeSystem')}
          </Button>
        </div>
      </div>

      <div className="form-group">
        <span className="section-label">{t('encounter.fields.mappingListLabel')}</span>
        <div className="repeat-list">
          {group.mappings.length === 0 && (
            <p className="form-hint form-hint--spaced">{t('encounter.fields.noMappings')}</p>
          )}
          {group.mappings.map(row => (
            <MappingRow
              key={row.rowKey}
              row={row}
              referenceCodes={referenceCodes}
              incomplete={incompleteRowKeys.has(row.rowKey)}
              onChange={rowPatch => onUpdateRow(row.rowKey, rowPatch)}
              onRemove={() => onRemoveRow(row.rowKey)} />
          ))}
        </div>
        <Button variant="secondary" size="sm" onClick={onAddRow}>
          {t('encounter.fields.addMapping')}
        </Button>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------- mapping row (local code -> CPT/SNOMED picker)

interface MappingRowProps {
  row: MappingRowState;
  referenceCodes: EncounterCode[];
  incomplete: boolean;
  onChange: (rowPatch: Partial<MappingRowState>) => void;
  onRemove: () => void;
}

function MappingRow({row, referenceCodes, incomplete, onChange, onRemove}: MappingRowProps) {
  const {t} = useTranslation('onboarding');
  const incompleteHintId = `encounter-row-hint-${row.rowKey}`;
  const selected = referenceCodes.find(code => code.system === row.targetSystem && code.code === row.targetCode);
  const [query, setQuery] = useState(selected ? referenceLabel(selected) : '');
  const [open, setOpen] = useState(false);
  const [highlightedIndex, setHighlightedIndex] = useState(-1);
  const blurTimeout = useRef<number>();
  const listboxId = `encounter-code-listbox-${row.rowKey}`;

  useEffect(() => {
    setQuery(selected ? referenceLabel(selected) : '');
    // Resync only when the resolved selection changes, not on every keystroke.
  }, [row.targetSystem, row.targetCode]);

  useEffect(() => () => window.clearTimeout(blurTimeout.current), []);

  const matches = useMemo(() => {
    const q = query.trim().toLowerCase();
    const pool = !q
      ? referenceCodes
      : referenceCodes.filter(code =>
          `${code.system} ${code.code} ${code.display} ${code.category ?? ''} ${code.categoryName ?? ''}`
            .toLowerCase()
            .includes(q)
        );
    return pool.slice(0, 25);
  }, [referenceCodes, query]);

  useEffect(() => {
    setHighlightedIndex(open && matches.length > 0 ? 0 : -1);
  }, [open, matches]);

  function selectMatch(code: EncounterCode) {
    onChange({targetSystem: code.system, targetCode: code.code, targetDisplay: code.display});
    setQuery(referenceLabel(code));
    setOpen(false);
  }

  function optionId(code: EncounterCode): string {
    return `${listboxId}-${encodeTarget(code.system, code.code)}`.replace(/[^\w-]/g, '-');
  }

  function handleTargetCodeKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }
      setHighlightedIndex(current => (matches.length === 0 ? -1 : (current + 1) % matches.length));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) {
        setOpen(true);
        return;
      }
      setHighlightedIndex(current => (matches.length === 0 ? -1 : (current - 1 + matches.length) % matches.length));
    } else if (event.key === 'Enter') {
      if (open && highlightedIndex >= 0 && highlightedIndex < matches.length) {
        event.preventDefault();
        selectMatch(matches[highlightedIndex]);
      }
    } else if (event.key === 'Escape') {
      if (open) {
        event.preventDefault();
        setOpen(false);
        setQuery(selected ? referenceLabel(selected) : '');
      }
    }
  }

  return (
    <div className={incomplete ? 'repeat-row repeat-row--incomplete' : 'repeat-row'}>
      <input
        type="text"
        aria-label={t('encounter.fields.localCodeLabel')}
        aria-describedby={incomplete ? incompleteHintId : undefined}
        placeholder={t('encounter.fields.localCodePlaceholder') ?? ''}
        value={row.localValue}
        onChange={event => onChange({localValue: event.target.value})} />

      <div className="encounter-code-picker">
        <input
          type="text"
          role="combobox"
          aria-label={t('encounter.fields.targetCodeLabel')}
          aria-describedby={incomplete ? incompleteHintId : undefined}
          aria-expanded={open}
          aria-controls={listboxId}
          aria-autocomplete="list"
          aria-activedescendant={
            open && highlightedIndex >= 0 && highlightedIndex < matches.length ? optionId(matches[highlightedIndex]) : undefined
          }
          placeholder={t('encounter.fields.targetCodePlaceholder') ?? ''}
          value={query}
          onFocus={event => {
            event.target.select();
            setOpen(true);
          }}
          onChange={event => {
            setQuery(event.target.value);
            setOpen(true);
          }}
          onKeyDown={handleTargetCodeKeyDown}
          onBlur={() => {
            blurTimeout.current = window.setTimeout(() => {
              setOpen(false);
              setQuery(selected ? referenceLabel(selected) : '');
            }, 150);
          }} />
        {open && (
          <div id={listboxId} className="encounter-code-dropdown" role="listbox">
            {matches.length === 0 ? (
              <div className="encounter-code-option encounter-code-option-empty">
                {t('encounter.fields.targetCodeNoMatches')}
              </div>
            ) : (
              matches.map((code, index) => (
                <div
                  key={encodeTarget(code.system, code.code)}
                  id={optionId(code)}
                  role="option"
                  aria-selected={code.system === row.targetSystem && code.code === row.targetCode}
                  className={
                    index === highlightedIndex ? 'encounter-code-option encounter-code-option--highlighted' : 'encounter-code-option'
                  }
                  onMouseEnter={() => setHighlightedIndex(index)}
                  onMouseDown={event => {
                    event.preventDefault();
                    selectMatch(code);
                  }}>
                  <span className="encounter-code-option-system">{systemBadgeLabel(code.system)}</span>{' '}
                  <span className="encounter-code-option-code">{code.code}</span> — {code.display}
                </div>
              ))
            )}
          </div>
        )}
      </div>

      {incomplete && (
        <span id={incompleteHintId} className="encounter-row-hint">
          {t('encounter.fields.incompleteRowHint')}
        </span>
      )}

      <Button
        variant="secondary"
        size="sm"
        onClick={onRemove}
        aria-label={
          row.localValue ? t('encounter.fields.removeMappingAriaLabel', {code: row.localValue}) : t('encounter.fields.removeMapping')
        }>
        {t('encounter.fields.removeMapping')}
      </Button>
    </div>
  );
}

// ---------------------------------------------------------------- helpers

function systemBadgeVariant(system: string): 'cpt' | 'snomed' | 'other' {
  const lowered = system.toLowerCase();
  return lowered.includes('cpt') ? 'cpt' : lowered.includes('snomed') ? 'snomed' : 'other';
}

/** Short pill text — the badge is a compact classifier, not the full system identifier (kept nearby as plain text where it matters). */
function systemBadgeLabel(system: string): string {
  const variant = systemBadgeVariant(system);
  return variant === 'other' ? system : variant.toUpperCase();
}

function referenceLabel(code: EncounterCode): string {
  // Shown as the picker's query text once a code is selected — the short mnemonic (CPT/SNOMED)
  // reads far better there than the FHIR canonical url code.system actually holds.
  return `${systemBadgeLabel(code.system)} ${code.code} — ${code.display}`;
}

/** Exported for the Report Details "Encounter Mapping" modal, which encodes a new mapping's target the same way. */
export function encodeTarget(system: string, code: string): string {
  return system && code ? `${system}|${code}` : '';
}

/** Exported for the Report Details "Encounter Mapping" modal, which decodes the same `encounterType` shape read-only. */
export function decodeTarget(value: string): [string, string] {
  const separatorIndex = value.indexOf('|');
  if (separatorIndex === -1) {
    return ['', ''];
  }
  return [value.slice(0, separatorIndex), value.slice(separatorIndex + 1)];
}

function makeKey(): string {
  return crypto.randomUUID();
}

/** Exported for the Report Details "Encounter Mapping" modal, which renders the same grouping read-only. */
export function buildGroups(codeSystems: string[], mappings: EncounterMapping[]): CodeSystemGroupState[] {
  const bySystem = new Map<string, MappingRowState[]>();
  mappings.forEach(mapping => {
    const [targetSystem, targetCode] = decodeTarget(mapping.encounterType);
    const rows = bySystem.get(mapping.system) ?? [];
    rows.push({rowKey: makeKey(), localValue: mapping.code, targetSystem, targetCode, targetDisplay: mapping.display ?? ''});
    bySystem.set(mapping.system, rows);
  });

  const order = [...codeSystems];
  bySystem.forEach((_rows, system) => {
    if (!order.includes(system)) {
      order.push(system);
    }
  });

  return order.map(codeSystem => ({
    groupKey: makeKey(),
    codeSystem,
    mappings: bySystem.get(codeSystem) ?? []
  }));
}

function flattenGroups(groups: CodeSystemGroupState[]): {codeSystems: string[]; mappings: EncounterMapping[]} {
  const codeSystems = groups.map(group => group.codeSystem);
  const mappings: EncounterMapping[] = [];
  groups.forEach(group => {
    group.mappings.forEach(row => {
      mappings.push({
        system: group.codeSystem,
        code: row.localValue,
        display: row.targetDisplay || undefined,
        encounterType: encodeTarget(row.targetSystem, row.targetCode)
      });
    });
  });
  return {codeSystems, mappings};
}
