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
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../api/ApiClientContext';
import type {DraftEnvelope} from '../api/ApiClient';
import {HttpError} from '../api/http';
import type {CommitResult, UserInfoResponse, VendorProfile} from '../api/contracts';
import {acronymTitle, Button, HeadingPause, Modal} from '../fields';
import {useNotifications} from '../notifications/NotificationProvider';
import {furthestLegalStep, isUnlocked, nextStepId, previousStepId, resolveStep} from './gating';
import {buildStepPath, parseStepPath, sameTarget} from './navigation';
import {draftReducer, type DraftAction, type DraftSections} from './reducer';
import {useStableCallback} from './StepChrome';
import {createEmptyDraft, migrateDraft, type FacilityDraft, type StepId, type StepTarget, type StepView} from './types';

type LoadState = 'loading' | 'ready' | 'error';

/**
 * A step whose editable data doesn't live in `FacilityDraft` (it's normalized server-side, saved
 * through its own endpoint - currently only MrnIntakeStep) registers one of these so the unsaved-
 * changes guard (`goTo`, driving both the sidebar and Back) still knows about it: `patch()`'s
 * `dirtyRef` alone would never see this step's edits, so without this the guard would let the user
 * navigate away, and "Save and Continue" would have nothing of this step's own to save.
 */
export interface StepUnsavedChanges {
  isDirty: () => boolean;
  /** Resolves false (after showing its own error) if the save failed - navigation is cancelled. */
  save: () => Promise<boolean>;
  /** Reverts the step's local state to what `save` last wrote (or what it loaded, if never saved). */
  discard: () => void;
}

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
  registerStepUnsavedChanges: (handler: StepUnsavedChanges | null) => void;
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

/** See `StepUnsavedChanges` - only a step whose data isn't part of `FacilityDraft` needs this. */
export function useStepUnsavedChanges(handler: StepUnsavedChanges) {
  const {registerStepUnsavedChanges} = useOnboarding();
  const stableIsDirty = useStableCallback(handler.isDirty);
  const stableSave = useStableCallback(handler.save);
  const stableDiscard = useStableCallback(handler.discard);
  useEffect(() => {
    registerStepUnsavedChanges({isDirty: stableIsDirty, save: stableSave, discard: stableDiscard});
    return () => registerStepUnsavedChanges(null);
  }, [registerStepUnsavedChanges, stableIsDirty, stableSave, stableDiscard]);
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
  const queryClient = useQueryClient();
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
  const stepUnsavedRef = useRef<StepUnsavedChanges | null>(null);
  const [pendingStepId, setPendingStepId] = useState<StepId | null>(null);
  const [errorStepIds, setErrorStepIdsState] = useState<ReadonlySet<StepId>>(() => new Set());
  const [saveAnnouncement, setSaveAnnouncement] = useState('');

  useEffect(() => {
    if (!saveAnnouncement) {
      return;
    }
    const timeoutId = window.setTimeout(() => setSaveAnnouncement(''), 1000);
    return () => window.clearTimeout(timeoutId);
  }, [saveAnnouncement]);

  const setErrorStepIds = useCallback((stepIds: Iterable<StepId>) => {
    setErrorStepIdsState(new Set(stepIds));
  }, []);

  const registerStepValidator = useCallback((validate: (() => boolean) | null) => {
    activeValidatorRef.current = validate;
  }, []);

  const registerStepUnsavedChanges = useCallback((handler: StepUnsavedChanges | null) => {
    stepUnsavedRef.current = handler;
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

  // Resolved once here so gating and the context value read the same thing.
  const vendorProfile = useMemo(
    () => vendorProfiles.find(profile => profile.vendor === draft.facilityInfo.vendor),
    [vendorProfiles, draft.facilityInfo.vendor]
  );

  // Every source of truth passes through one gate.
  const target = useMemo<StepTarget>(() => {
    if (loadState !== 'ready') {
      return {stepId: draft.currentStepId};
    }
    const preferred: StepTarget =
      urlTarget ?? {stepId: draft.currentStepId, view: draft.currentView};
    return resolveStep(preferred, draft, user, vendorProfile);
  }, [loadState, urlTarget, draft, user, vendorProfile]);

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

      const previousVendor = lastSavedDraftRef.current?.facilityInfo.vendor;
      const previousFhirBaseUrl = lastSavedDraftRef.current?.fhir.fhirServerBaseUrl?.trim();

      const outcome = saveChain.current
        .catch(() => undefined)
        .then(async () => {
          try {
            await api.saveDraft(toSave);
            lastSavedDraftRef.current = toSave;
            dirtyRef.current = false;

            const vendorChanged = Boolean(
              previousVendor && toSave.facilityInfo.vendor && previousVendor !== toSave.facilityInfo.vendor
            );
            const fhirBaseUrlChanged = Boolean(
              previousFhirBaseUrl &&
                toSave.fhir.fhirServerBaseUrl &&
                previousFhirBaseUrl !== toSave.fhir.fhirServerBaseUrl.trim()
            );

            if (vendorChanged || fhirBaseUrlChanged) {
              // The BFF just revoked the census step's accuracy acknowledgement (and the latest
              // report's) server-side as a side effect of this vendor or FHIR base URL change -
              // reload so the now-pending census (and every step gated behind it) shows up instead
              // of the stale in-memory draft. A failed reload here shouldn't surface as a save
              // failure - the save itself succeeded.
              try {
                await reloadDraft();
              } catch {
                // Next navigation/reload picks up the server's state regardless.
              }

              // Report Results' own acknowledgement isn't part of the draft - it's a separate,
              // staleTime:Infinity query keyed by report id (see ReportResultsStep) that reloadDraft
              // never touches. The report id itself didn't change, so without this the checkbox
              // would keep showing the stale "accepted" value the BFF just revoked.
              await queryClient.invalidateQueries({queryKey: ['reportAccuracyAcknowledgement']});
            }

            setSaveAnnouncement(t('status.saved'));
            return true;
          } catch (cause) {
            const translationKey = cause instanceof HttpError && cause.errorCode
              ? SAVE_ERROR_CODE_KEYS[cause.errorCode]
              : undefined;
            // Only an HttpError's message is written for a facility to read (it's the BFF's own
            // ProblemDetails detail/title). Anything else - a TimeoutError, a network drop - is a
            // raw technical string (e.g. "Request timed out after 30000ms."), so it falls back to
            // the same generic, friendly message a non-Error rejection already gets.
            notifyError(
              translationKey
                ? t(translationKey)
                : cause instanceof HttpError
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
    [api, notifyError, t, reloadDraft, queryClient]
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
      if (dirtyRef.current || Boolean(stepUnsavedRef.current?.isDirty())) {
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
    const stepHandler = stepUnsavedRef.current;
    const stepSaved = stepHandler ? stepHandler.save() : Promise.resolve(true);
    stepSaved.then(ok => {
      if (!ok) {
        return;
      }
      persistDraft(draft).then(saved => {
        if (saved) {
          completeGoTo(stepId);
        }
      });
    });
  }, [pendingStepId, draft, persistDraft, completeGoTo, notifyError, t]);

  const confirmDiscardChanges = useCallback(() => {
    if (pendingStepId === null) {
      return;
    }
    const stepId = pendingStepId;
    setPendingStepId(null);
    stepUnsavedRef.current?.discard();
    const savedContent = lastSavedDraftRef.current;
    // Only the section fields the user just edited are "dirty" -- which steps are
    // unlocked never goes through patch(), so it never gets saved on a transition that
    // has nothing else to save (e.g. leaving a step with no editable fields). Reverting
    // to lastSavedDraftRef's unlockedStepIds wholesale would then relock every step
    // reached that way, sending Discard Changes back further than the one step the user
    // asked for. The current draft's navigation state is what actually reflects where
    // the user has legitimately been, so it rides along unchanged.
    const restored: FacilityDraft | undefined = savedContent && {
      ...savedContent,
      unlockedStepIds: draft.unlockedStepIds
    };
    if (restored) {
      dispatch({type: 'draft/loaded', draft: restored});
    }
    dirtyRef.current = false;
    queryClient.invalidateQueries({queryKey: ['reportAccuracyAcknowledgement']});
    // Against restored's own vendor - a discard can revert an in-flight vendor change.
    const restoredVendorProfile = restored
      ? vendorProfiles.find(profile => profile.vendor === restored.facilityInfo.vendor)
      : undefined;
    const target =
      restored && !isUnlocked(stepId, restored, user, restoredVendorProfile)
        ? furthestLegalStep(restored, user, restoredVendorProfile)
        : stepId;
    if (target !== stepId) {
      notifyError(t('unsavedChanges.messages.discardRelocked'));
    }
    completeGoTo(target);
  }, [pendingStepId, draft.unlockedStepIds, completeGoTo, user, notifyError, t, vendorProfiles, queryClient]);

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
      registerStepValidator,
      registerStepUnsavedChanges
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
      registerStepUnsavedChanges,
      reloadDraft
    ]
  );

  return (
    <OnboardingContext.Provider value={value}>
      {children}
      <div className="nhsn-link__visually-hidden" role="status">
        {saveAnnouncement}
      </div>
      <Modal
        open={pendingStepId !== null}
        title={acronymTitle(<HeadingPause>{t('unsavedChanges.title')}</HeadingPause>)}
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
