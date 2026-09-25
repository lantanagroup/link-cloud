import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNotifications } from '../../../notifications/NotificationProvider';

/**
 * Shared shape behind the HSLOC and Encounter mapping editors in the per-patient mapping
 * evidence modals: a reference code list (fetched once and reused), the facility's live
 * mappings (always refetched on open, since they can change between openings -- including from
 * this same hook's own `add`), and the in-progress "+ Add Mapping" selections for unmapped
 * values, keyed by whatever key the caller uses to identify an unmapped row (a bare code for
 * HSLOC, a `system|code` composite for Encounter). HSLOC and Encounter differ in mapping shape
 * and in how a save fans out (HSLOC also writes the `hslocMappings` query cache HslocStep reads;
 * Encounter only mirrors the draft, since EncounterStep sources its mappings from the draft, not
 * a query) -- callers supply that as config rather than the hook guessing at it.
 */
export interface UseMappingEvidenceConfig<TCode, TMapping> {
  loadCodes: () => Promise<TCode[]>;
  loadMappings: () => Promise<TMapping[]>;
  saveMappings: (mappings: TMapping[]) => Promise<void>;
  onSaved?: (mappings: TMapping[]) => void;
  successMessageKey: string;
}

export interface MappingEvidence<TCode, TMapping> {
  codes: TCode[];
  mappings: TMapping[];
  dataLoading: boolean;
  selections: Record<string, string>;
  addingKey: string | null;
  setSelection: (key: string, value: string) => void;
  resetSelections: () => void;
  load: () => Promise<void>;
  add: (key: string, mapping: TMapping) => Promise<void>;
}

export function useMappingEvidence<TCode, TMapping>(
  config: UseMappingEvidenceConfig<TCode, TMapping>,
): MappingEvidence<TCode, TMapping> {
  const { t } = useTranslation(['onboarding', 'common']);
  const { notifySuccess, notifyError } = useNotifications();
  const [codes, setCodes] = useState<TCode[]>([]);
  const [mappings, setMappings] = useState<TMapping[]>([]);
  const [dataLoading, setDataLoading] = useState(false);
  const [selections, setSelections] = useState<Record<string, string>>({});
  const [addingKey, setAddingKey] = useState<string | null>(null);

  function loadErrorMessage(cause: unknown): string {
    return cause instanceof Error
      ? cause.message
      : t('onboarding:reportResults.messages.loadError');
  }

  async function load() {
    setDataLoading(true);
    try {
      const [nextCodes, nextMappings] = await Promise.all([
        codes.length === 0 ? config.loadCodes() : Promise.resolve(codes),
        config.loadMappings(),
      ]);
      setCodes(nextCodes);
      setMappings(nextMappings);
    } catch (cause) {
      notifyError(loadErrorMessage(cause));
    } finally {
      setDataLoading(false);
    }
  }

  function setSelection(key: string, value: string) {
    setSelections((prev) => ({ ...prev, [key]: value }));
  }

  function resetSelections() {
    setSelections({});
  }

  async function add(key: string, mapping: TMapping) {
    setAddingKey(key);
    try {
      const nextMappings = [...mappings, mapping];
      await config.saveMappings(nextMappings);
      setMappings(nextMappings);
      config.onSaved?.(nextMappings);
      notifySuccess(t(config.successMessageKey));
      setSelections((prev) => {
        const next = { ...prev };
        delete next[key];
        return next;
      });
    } catch (cause) {
      notifyError(loadErrorMessage(cause));
    } finally {
      setAddingKey(null);
    }
  }

  return {
    codes,
    mappings,
    dataLoading,
    selections,
    addingKey,
    setSelection,
    resetSelections,
    load,
    add,
  };
}
