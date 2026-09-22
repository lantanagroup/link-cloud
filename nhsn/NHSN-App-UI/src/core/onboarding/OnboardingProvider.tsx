import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
  useTransition
} from 'react';
import {useQuery} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../api/ApiClientContext';
import type {DraftEnvelope} from '../api/ApiClient';
import {HttpError} from '../api/http';
import type {CommitResult, UserInfoResponse, VendorProfile} from '../api/contracts';
import {Button, Modal} from '../fields';
import {useNotifications} from '../notifications/NotificationProvider';
import {furthestLegalStep, nextStepId, previousStepId, resolveStep} from './gating';
import {buildStepPath, parseStepPath, sameTarget} from './navigation';
import {draftReducer, type DraftAction, type DraftSections} from './reducer';
import {useStableCallback} from './StepChrome';
import {createEmptyDraft, migrateDraft, type FacilityDraft, type StepId, type StepTarget, type StepView} from './types';

type LoadState = 'loading' | 'ready' | 'error';

// Maps a BFF ProblemDetails errorCode (HttpError.errorCode) to a translation key, so a save
// rejection shows a localized message instead of the BFF's English-only Detail text. Codes with no
// entry here fall back to that raw Detail - see persistDraft below.
const SAVE_ERROR_CODE_KEYS: Record<string, string> = {
  invalidFhirServerBaseUrl: 'onboarding:fhirServerInfo.messages.invalidBaseUrl',
  invalidPatientListConfiguration: 'onboarding:census.messages.saveRejected'
};

interface OnboardingContextValue {
  loadState: LoadState;
  error?: string;
  draft: FacilityDraft;
  user: UserInfoResponse;
  target: StepTarget;
  vendorProfile?: VendorProfile;
  commitState: CommitResult | null;
  /** Navigates back to the home route once enrollment is complete. */
  goHome: () => void;
  /** True while a save is in flight or the next step's code is still loading; steps disable their Next button on it. */
  saving: boolean;
  savingDirection: 'next' | 'back' | null;

  patch: <K extends keyof DraftSections>(section: K, patch: Partial<DraftSections[K]>) => void;
  mirror: <K extends keyof DraftSections>(section: K, patch: Partial<DraftSections[K]>) => void;
  /** Saves the current draft immediately, outside the normal step-transition save. Resolves false
   *  (and shows the usual save-rejection notification) if the save failed. */
  save: () => Promise<boolean>;
  /** Steps flagged with a validation error (currently: sections a failed manual-upload import
   *  touched) - drives the red exclamation mark next to a step's name in the nav. */
  errorStepIds: ReadonlySet<StepId>;
  /** Replaces the whole set - each new import attempt should reflect only its own errors. */
  setErrorStepIds: (stepIds: Iterable<StepId>) => void;
  goTo: (stepId: StepId) => void;
  goNext: () => void;
  goBack: () => void;
  openView: (view: StepView) => void;
  closeView: () => void;
  reloadDraft: () => Promise<void>;
  dispatch: React.Dispatch<DraftAction>;
  registerStepValidator: (validate: (() => boolean) | null) => void;
}

const OnboardingContext = createContext<OnboardingContextValue | null>(null);

export function useOnboarding(): OnboardingContextValue {
  const value = useContext(OnboardingContext);
  if (!value) {
    throw new Error('useOnboarding used outside OnboardingProvider');
  }
  return value;
}

/** Selector hook so a step re-renders on its own slice rather than the whole draft. */
export function useDraftSection<K extends keyof DraftSections>(section: K): DraftSections[K] {
  return useOnboarding().draft[section] as DraftSections[K];
}

export function useStepValidator(validate: () => boolean) {
  const {registerStepValidator} = useOnboarding();
  const stableValidate = useStableCallback(validate);
  useEffect(() => {
    registerStepValidator(stableValidate);
    return () => registerStepValidator(null);
  }, [registerStepValidator, stableValidate]);
}

export function OnboardingProvider({
  user,
  baseUrl,
  onGoHome,
  children
}: {
  user: UserInfoResponse;
  baseUrl: string;
  onGoHome: () => void;
  children: React.ReactNode;
}) {
  const api = useApiClient();
  const {t} = useTranslation('common');
  const {notifyError} = useNotifications();
  const [draft, dispatch] = useReducer(draftReducer, undefined, createEmptyDraft);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [error, setError] = useState<string>();
  const [commitState, setCommitState] = useState<CommitResult | null>(null);
  const [draftReady, setDraftReady] = useState(false);
  const {
    data: vendorProfiles = [],
    isLoading: vendorProfilesLoading,
    error: vendorProfilesError
  } = useQuery({
    queryKey: ['vendorProfiles'],
    queryFn: () => api.getVendorProfiles(),
    staleTime: Infinity
  });
  const [saving, setSaving] = useState(false);
  const [navDirection, setNavDirection] = useState<'next' | 'back' | null>(null);
  const [isStepPending, startStepTransition] = useTransition();
  const saveChain = useRef<Promise<unknown>>(Promise.resolve());
  const pendingSaves = useRef(0);
  const dirtyRef = useRef(false);
  const lastSavedDraftRef = useRef<FacilityDraft>();
  const activeValidatorRef = useRef<(() => boolean) | null>(null);
  const [pendingStepId, setPendingStepId] = useState<StepId | null>(null);
  const [errorStepIds, setErrorStepIdsState] = useState<ReadonlySet<StepId>>(() => new Set());

  const setErrorStepIds = useCallback((stepIds: Iterable<StepId>) => {
    setErrorStepIdsState(new Set(stepIds));
  }, []);

  const registerStepValidator = useCallback((validate: (() => boolean) | null) => {
    activeValidatorRef.current = validate;
  }, []);

  const applyEnvelope = useCallback((envelope: DraftEnvelope) => {
    setCommitState(envelope.commitState);
    const loaded = migrateDraft(envelope.draft ?? createEmptyDraft());
    lastSavedDraftRef.current = loaded;
    dirtyRef.current = false;
    dispatch({type: 'draft/loaded', draft: loaded});
  }, []);

  const reloadDraft = useCallback(async () => {
    const envelope = await api.getDraft();
    applyEnvelope(envelope);
  }, [api, applyEnvelope]);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const envelope = await api.getDraft();
        if (!active) {
          return;
        }
        applyEnvelope(envelope);
        setDraftReady(true);
      } catch (cause) {
        if (!active) {
          return;
        }
        setError(cause instanceof Error ? cause.message : String(cause));
        setLoadState('error');
      }
    })();
    return () => {
      active = false;
    };
  }, [api, applyEnvelope]);

  // A step must never see a half-loaded context, so 'ready' waits on both the
  // draft and vendor profiles - whichever settles last decides the moment.
  useEffect(() => {
    if (!draftReady || vendorProfilesLoading) {
      return;
    }
    if (vendorProfilesError) {
      setError(vendorProfilesError instanceof Error ? vendorProfilesError.message : String(vendorProfilesError));
      setLoadState('error');
      return;
    }
    setLoadState('ready');
  }, [draftReady, vendorProfilesLoading, vendorProfilesError]);

  // Where the URL says we are, when it parses to one of ours. On first mount
  // this is the deep link; afterwards it is whatever popstate last produced.
  const [urlTarget, setUrlTarget] = useState<StepTarget | undefined>(() =>
    parseStepPath(window.location.pathname, baseUrl)
  );

  useEffect(() => {
    const onPopState = () => {
      const parsed = parseStepPath(window.location.pathname, baseUrl);
      // Not ours — the host navigated. Leave the wizard alone.
      if (parsed) {
        setUrlTarget(parsed);
      }
    };
    window.addEventListener('popstate', onPopState);
    return () => window.removeEventListener('popstate', onPopState);
  }, [baseUrl]);

  // Every source of truth passes through one gate.
  const target = useMemo<StepTarget>(() => {
    if (loadState !== 'ready') {
      return {stepId: draft.currentStepId};
    }
    const preferred: StepTarget =
      urlTarget ?? {stepId: draft.currentStepId, view: draft.currentView};
    return resolveStep(preferred, draft, user);
  }, [loadState, urlTarget, draft, user]);

  // Mirror the resolved target into the URL and the draft. Doing it after
  // resolution rather than at the call site means a rejected deep link
  // corrects the address bar instead of leaving a lie in it.
  const lastWritten = useRef<StepTarget>();
  useEffect(() => {
    if (loadState !== 'ready') {
      return;
    }
    if (sameTarget(lastWritten.current, target)) {
      return;
    }
    lastWritten.current = target;

    const path = buildStepPath(target, baseUrl);
    if (window.location.pathname !== path) {
      window.history.pushState({nhsnLinkStep: target.stepId}, '', path);
    }

    if (draft.currentStepId !== target.stepId || draft.currentView?.view !== target.view?.view) {
      dispatch(
        target.view ? {type: 'view/open', view: target.view} : {type: 'step/goto', stepId: target.stepId}
      );
    }
  }, [target, baseUrl, loadState, draft.currentStepId, draft.currentView]);

  const persistDraft = useCallback(
    (toSave: FacilityDraft): Promise<boolean> => {
      pendingSaves.current += 1;
      setSaving(true);

      const outcome = saveChain.current
        .catch(() => undefined)
        .then(async () => {
          try {
            await api.saveDraft(toSave);
            lastSavedDraftRef.current = toSave;
            dirtyRef.current = false;
            return true;
          } catch (cause) {
            const translationKey = cause instanceof HttpError && cause.errorCode
              ? SAVE_ERROR_CODE_KEYS[cause.errorCode]
              : undefined;
            notifyError(
              translationKey
                ? t(translationKey)
                : cause instanceof Error
                  ? cause.message
                  : t('errors.saveFailed')
            );
            return false;
          } finally {
            pendingSaves.current -= 1;
            if (pendingSaves.current === 0) {
              setSaving(false);
            }
          }
        });
      saveChain.current = outcome;
      return outcome;
    },
    [api, notifyError, t]
  );

  // Persist at transitions. popstate cannot be cancelled, so a dirty-navigation
  // prompt cannot guard the back button the way beforeunload guards a reload —
  // saving on transition is what keeps a later reload agreeing with where back
  // went.
  const persistedStep = useRef<string>();
  useEffect(() => {
    if (loadState !== 'ready') {
      return;
    }
    const key = `${draft.currentStepId}:${draft.currentView?.view ?? ''}`;
    if (persistedStep.current === key) {
      return;
    }
    persistedStep.current = key;
    if (dirtyRef.current) {
      persistDraft(draft);
    }
  }, [draft, loadState, persistDraft]);

  const completeGoTo = useCallback((stepId: StepId) => {
    startStepTransition(() => {
      setUrlTarget(undefined);
      dispatch({type: 'step/unlock', stepId});
      dispatch({type: 'step/goto', stepId});
    });
  }, []);

  const goTo = useCallback(
    (stepId: StepId) => {
      if (dirtyRef.current) {
        setPendingStepId(stepId);
        return;
      }
      completeGoTo(stepId);
    },
    [completeGoTo]
  );

  const confirmSaveAndContinue = useCallback(() => {
    if (pendingStepId === null) {
      return;
    }
    const stepId = pendingStepId;
    setPendingStepId(null);
    if (activeValidatorRef.current && !activeValidatorRef.current()) {
      notifyError(t('unsavedChanges.messages.incomplete'));
      return;
    }
    persistDraft(draft).then(saved => {
      if (saved) {
        completeGoTo(stepId);
      }
    });
  }, [pendingStepId, draft, persistDraft, completeGoTo, notifyError, t]);

  const confirmDiscardChanges = useCallback(() => {
    if (pendingStepId === null) {
      return;
    }
    const stepId = pendingStepId;
    setPendingStepId(null);
    const restored = lastSavedDraftRef.current;
    if (restored) {
      dispatch({type: 'draft/loaded', draft: restored});
    }
    dirtyRef.current = false;
    completeGoTo(stepId);
  }, [pendingStepId, completeGoTo]);

  const advanceTo = useCallback(
    (stepId: StepId, direction: 'next' | 'back') => {
      if (!dirtyRef.current) {
        completeGoTo(stepId);
        return;
      }
      setNavDirection(direction);
      persistDraft(draft).then(saved => {
        setNavDirection(null);
        if (saved) {
          completeGoTo(stepId);
        }
      });
    },
    [draft, persistDraft, completeGoTo]
  );

  const goNext = useCallback(() => {
    const next = nextStepId(target.stepId, draft, user);
    if (next) {
      advanceTo(next, 'next');
    }
  }, [target.stepId, draft, user, advanceTo]);

  const goBack = useCallback(() => {
    if (target.view) {
      setUrlTarget(undefined);
      dispatch({type: 'view/close'});
      return;
    }
    const previous = previousStepId(target.stepId, draft, user);
    if (previous) {
      goTo(previous);
    }
  }, [target, draft, user, goTo]);

  const openView = useCallback((view: StepView) => {
    setUrlTarget(undefined);
    dispatch({type: 'view/open', view});
  }, []);

  const closeView = useCallback(() => {
    setUrlTarget(undefined);
    dispatch({type: 'view/close'});
  }, []);

  const patch = useCallback<OnboardingContextValue['patch']>((section, sectionPatch) => {
    dirtyRef.current = true;
    dispatch({type: 'section/patch', section, patch: sectionPatch});
  }, []);

  const mirror = useCallback<OnboardingContextValue['mirror']>((section, sectionPatch) => {
    dispatch({type: 'section/patch', section, patch: sectionPatch});
  }, []);

  const save = useCallback(() => persistDraft(draft), [persistDraft, draft]);

  const vendorProfile = useMemo(
    () => vendorProfiles.find(profile => profile.vendor === draft.facilityInfo.vendor),
    [vendorProfiles, draft.facilityInfo.vendor]
  );

  const value = useMemo<OnboardingContextValue>(
    () => ({
      loadState,
      error,
      draft,
      user,
      target,
      vendorProfile,
      commitState,
      goHome: onGoHome,
      saving: saving || isStepPending,
      savingDirection: saving ? navDirection : null,
      patch,
      mirror,
      save,
      errorStepIds,
      setErrorStepIds,
      goTo,
      goNext,
      goBack,
      openView,
      closeView,
      reloadDraft,
      dispatch,
      registerStepValidator
    }),
    [
      loadState,
      error,
      draft,
      user,
      target,
      vendorProfile,
      commitState,
      onGoHome,
      saving,
      navDirection,
      isStepPending,
      patch,
      mirror,
      save,
      errorStepIds,
      setErrorStepIds,
      goTo,
      goNext,
      goBack,
      openView,
      closeView,
      registerStepValidator,
      reloadDraft
    ]
  );

  return (
    <OnboardingContext.Provider value={value}>
      {children}
      <Modal
        open={pendingStepId !== null}
        title={t('unsavedChanges.title')}
        onClose={() => setPendingStepId(null)}
        showCloseButton
        closeLabel={t('actions.close')}
        footer={
          <>
            <Button variant="secondary" onClick={confirmDiscardChanges}>
              {t('actions.discardChanges')}
            </Button>
            <Button onClick={confirmSaveAndContinue}>{t('actions.saveAndContinue')}</Button>
          </>
        }>
        <p>{t('unsavedChanges.message')}</p>
      </Modal>
    </OnboardingContext.Provider>
  );
}

export {furthestLegalStep};
