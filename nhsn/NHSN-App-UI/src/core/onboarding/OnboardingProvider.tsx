import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState
} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../api/ApiClientContext';
import type {DraftEnvelope} from '../api/ApiClient';
import type {CommitResult, UserInfoResponse, VendorProfile} from '../api/contracts';
import {useNotifications} from '../notifications/NotificationProvider';
import {furthestLegalStep, nextStepId, previousStepId, resolveStep} from './gating';
import {buildStepPath, parseStepPath, sameTarget} from './navigation';
import {draftReducer, type DraftAction, type DraftSections} from './reducer';
import {createEmptyDraft, migrateDraft, type FacilityDraft, type StepId, type StepTarget, type StepView} from './types';

type LoadState = 'loading' | 'ready' | 'error';

interface OnboardingContextValue {
  loadState: LoadState;
  error?: string;
  draft: FacilityDraft;
  user: UserInfoResponse;
  target: StepTarget;
  vendorProfile?: VendorProfile;
  commitState: CommitResult | null;
  /** Where "Return to Home" sends the browser once enrollment is complete — the host page, not a step. */
  homeUrl: string;
  /** True while a save is in flight; steps disable their Next button on it. */
  saving: boolean;

  patch: <K extends keyof DraftSections>(section: K, patch: Partial<DraftSections[K]>) => void;
  goTo: (stepId: StepId) => void;
  goNext: () => void;
  goBack: () => void;
  openView: (view: StepView) => void;
  closeView: () => void;
  reloadDraft: () => Promise<void>;
  dispatch: React.Dispatch<DraftAction>;
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

export function OnboardingProvider({
  user,
  baseUrl,
  homeUrl,
  children
}: {
  user: UserInfoResponse;
  baseUrl: string;
  homeUrl: string;
  children: React.ReactNode;
}) {
  const api = useApiClient();
  const {t} = useTranslation('common');
  const {notifyError} = useNotifications();
  const [draft, dispatch] = useReducer(draftReducer, undefined, createEmptyDraft);
  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [error, setError] = useState<string>();
  const [commitState, setCommitState] = useState<CommitResult | null>(null);
  const [vendorProfiles, setVendorProfiles] = useState<VendorProfile[]>([]);
  const [saving, setSaving] = useState(false);
  const saveChain = useRef<Promise<unknown>>(Promise.resolve());
  const pendingSaves = useRef(0);

  const applyEnvelope = useCallback((envelope: DraftEnvelope) => {
    setCommitState(envelope.commitState);
    dispatch({type: 'draft/loaded', draft: migrateDraft(envelope.draft ?? createEmptyDraft())});
  }, []);

  const reloadDraft = useCallback(async () => {
    const envelope = await api.getDraft();
    applyEnvelope(envelope);
  }, [api, applyEnvelope]);

  // Initial load. Vendor profiles come with it because every step that
  // branches reads them, and a step must never see a half-loaded context.
  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const [envelope, profiles] = await Promise.all([api.getDraft(), api.getVendorProfiles()]);
        if (!active) {
          return;
        }
        applyEnvelope(envelope);
        setVendorProfiles(profiles);
        setLoadState('ready');
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
            return true;
          } catch (cause) {
            notifyError(cause instanceof Error ? cause.message : t('errors.saveFailed'));
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
    persistDraft(draft);
  }, [draft, loadState, persistDraft]);

  const goTo = useCallback(
    (stepId: StepId) => {
      const nextDraft: FacilityDraft = {...draft, currentStepId: stepId, currentView: undefined};
      persistDraft(nextDraft).then(saved => {
        if (!saved) {
          return;
        }
        persistedStep.current = `${stepId}:`;
        setUrlTarget(undefined);
        dispatch({type: 'step/unlock', stepId});
        dispatch({type: 'step/goto', stepId});
      });
    },
    [draft, persistDraft]
  );

  const goNext = useCallback(() => {
    const next = nextStepId(target.stepId, draft, user);
    if (next) {
      goTo(next);
    }
  }, [target.stepId, draft, user, goTo]);

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
    dispatch({type: 'section/patch', section, patch: sectionPatch});
  }, []);

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
      homeUrl,
      saving,
      patch,
      goTo,
      goNext,
      goBack,
      openView,
      closeView,
      reloadDraft,
      dispatch
    }),
    [
      loadState,
      error,
      draft,
      user,
      target,
      vendorProfile,
      commitState,
      homeUrl,
      saving,
      patch,
      goTo,
      goNext,
      goBack,
      openView,
      closeView,
      reloadDraft
    ]
  );

  return <OnboardingContext.Provider value={value}>{children}</OnboardingContext.Provider>;
}

export {furthestLegalStep};
